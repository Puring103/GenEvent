using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GenEvent.SourceGenerator
{
    internal enum SubscriberPriority { Primary, High, Medium, Low, End }

    [Generator]
    public class GenEventSourceGenerator : ISourceGenerator
    {
        private const string GenEventMetadataName = "GenEvent.Interface.IGenEvent`1";
        private const string OnEventAttributeMetadataName = "GenEvent.OnEventAttribute";
        private const string TaskMetadataName = "System.Threading.Tasks.Task";
        private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                var compilation = context.Compilation;

                var iGenEventSymbol = compilation.GetTypeByMetadataName(GenEventMetadataName);
                var onEventAttributeSymbol = compilation.GetTypeByMetadataName(OnEventAttributeMetadataName);

                if (iGenEventSymbol == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("GE001", "Missing type", "IGenEvent interface not found. Ensure GenEvent is referenced.", "GenEvent", DiagnosticSeverity.Warning, true),
                        Location.None));
                    return;
                }

                if (onEventAttributeSymbol == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("GE002", "Missing type", "OnEventAttribute not found. Ensure GenEvent is referenced.", "GenEvent", DiagnosticSeverity.Warning, true),
                        Location.None));
                    return;
                }

                var (subscribers, diagnostics) = CollectSubscribers(compilation, onEventAttributeSymbol, context);
                var events = CollectEvents(compilation, subscribers);

                foreach (var d in diagnostics)
                    context.ReportDiagnostic(d);

                var eventToSubscribers = BuildEventSubscriberMap(subscribers);
                var subscriberToEvents = BuildSubscriberEventMap(subscribers);

                var eventPublisherTemplate = Templates.EventPublisher;
                var subscriberRegistryTemplate = Templates.SubscriberRegistry;
                var hasUnity = IsUnityProject(compilation);

                foreach (var evt in events)
                {
                    if (!eventToSubscribers.TryGetValue(evt.EventType, out var subList))
                        subList = new List<SubscriberInfo>();
                    var source = GenerateEventPublisher(evt, subList, eventPublisherTemplate);
                    context.AddSource($"{evt.GeneratedPublisherName}.g.cs", SourceText.From(source, Encoding.UTF8));
                }

                foreach (var sub in subscribers.GroupBy(s => s.SubscriberType, SymbolEqualityComparer.Default).Select(g => g.First()))
                {
                    if (!subscriberToEvents.TryGetValue(sub.SubscriberType, out var evtList))
                        evtList = new List<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool, bool IsAsync)>();
                    var source = GenerateSubscriberRegistry(sub, evtList, subscriberRegistryTemplate);
                    context.AddSource($"{GetGeneratedSubscriberRegistryName(sub.SubscriberType)}.g.cs", SourceText.From(source, Encoding.UTF8));
                }

                var publisherRegistrations = string.Join(Environment.NewLine,
                    events.Select(e => $"        BaseEventPublisher.Publishers[typeof({e.TypeDisplayName})] = new {e.GeneratedPublisherName}();"));
                var subscriberRegs = subscribers.GroupBy(s => s.SubscriberType, SymbolEqualityComparer.Default);
                var subscriberRegistrations = string.Join(Environment.NewLine,
                    subscriberRegs.Select(g =>
                    {
                        var subscriberType = (INamedTypeSymbol)g.Key;
                        return $"        BaseSubscriberRegistry.Subscribers[typeof({GetFullyQualifiedTypeName(subscriberType)})] = new {GetGeneratedSubscriberRegistryName(subscriberType)}();";
                    }));

                var initAttribute = hasUnity
                    ? "[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]"
                    : "";
                var bootstrapSource = Templates.GenEventBootstrap
                    .Replace("{InitAttribute}", initAttribute)
                    .Replace("{PublisherRegistrations}", publisherRegistrations)
                    .Replace("{SubscriberRegistrations}", subscriberRegistrations);
                context.AddSource("GenEventBootstrap.g.cs", SourceText.From(bootstrapSource, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("GE999", "Generator error", $"GenEvent source generator failed: {ex.Message}", "GenEvent", DiagnosticSeverity.Error, true),
                    Location.None));
            }
        }

        /// <summary>
        /// 检测当前编译是否为 Unity 项目（引用 UnityEngine 或 UnityEditor），
        /// 以便为 GenEventBootstrap.Init 自动添加 [RuntimeInitializeOnLoadMethod]。
        /// </summary>
        private static bool IsUnityProject(Compilation compilation)
        {
            foreach (var name in compilation.ReferencedAssemblyNames)
            {
                var n = name.Name;
                if (n != null && (n.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static List<EventInfo> CollectEvents(Compilation compilation, IReadOnlyList<SubscriberInfo> subscribers)
        {
            var result = new List<EventInfo>();
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            CollectEventsFromNamespace(compilation.Assembly.GlobalNamespace, compilation, result, seen);

            foreach (var externalEventType in subscribers
                         .Select(s => s.EventType)
                         .Where(t => !SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, compilation.Assembly)))
            {
                AddEventInfo(externalEventType, result, seen);
            }

            return result;
        }

        private static void CollectEventsFromNamespace(
            INamespaceSymbol ns,
            Compilation compilation,
            List<EventInfo> result,
            HashSet<INamedTypeSymbol> seen)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                CollectEventsFromType(type, compilation, result, seen);
            }

            foreach (var childNamespace in ns.GetNamespaceMembers())
            {
                CollectEventsFromNamespace(childNamespace, compilation, result, seen);
            }
        }

        private static void CollectEventsFromType(
            INamedTypeSymbol type,
            Compilation compilation,
            List<EventInfo> result,
            HashSet<INamedTypeSymbol> seen)
        {
            if (type.TypeKind == TypeKind.Struct && ImplementsIGenEvent(type, compilation))
                AddEventInfo(type, result, seen);

            foreach (var nestedType in type.GetTypeMembers())
            {
                CollectEventsFromType(nestedType, compilation, result, seen);
            }
        }

        private static void AddEventInfo(INamedTypeSymbol type, List<EventInfo> result, HashSet<INamedTypeSymbol> seen)
        {
            if (!seen.Add(type))
                return;

            result.Add(new EventInfo
            {
                EventType = type,
                TypeDisplayName = GetFullyQualifiedTypeName(type),
                GeneratedPublisherName = GetGeneratedPublisherName(type)
            });
        }

        private static (List<SubscriberInfo> subscribers, List<Diagnostic> diagnostics) CollectSubscribers(
            Compilation compilation, INamedTypeSymbol onEventAttributeSymbol, GeneratorExecutionContext context)
        {
            var subscribers = new List<SubscriberInfo>();
            var diagnostics = new List<Diagnostic>();
            // Per (class, eventType): list of (method, location, isAsync, priority). At most one sync and one async allowed.
            var eventMethodsByClass = new Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol, List<(IMethodSymbol method, Location loc, bool isAsync, SubscriberPriority priority)>>>(SymbolEqualityComparer.Default);

            var taskSymbol = compilation.GetTypeByMetadataName(TaskMetadataName);
            var taskOfTSymbol = compilation.GetTypeByMetadataName(TaskOfTMetadataName);

            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = tree.GetRoot();
                var model = compilation.GetSemanticModel(tree);

                var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                foreach (var methodDecl in methodDeclarations)
                {
                    var methodSymbol = model.GetDeclaredSymbol(methodDecl, context.CancellationToken) as IMethodSymbol;
                    if (methodSymbol == null) continue;

                    var onEventAttr = methodSymbol.GetAttributes()
                        .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, onEventAttributeSymbol));
                    if (onEventAttr == null) continue;

                    if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE010", "Invalid OnEvent", "[OnEvent] methods must be public.", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }

                    if (methodSymbol.Parameters.Length != 1)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE011", "Invalid OnEvent", "[OnEvent] methods must have exactly one parameter (the event type).", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }

                    var paramType = methodSymbol.Parameters[0].Type as INamedTypeSymbol;
                    if (paramType == null || !ImplementsIGenEvent(paramType, compilation))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE012", "Invalid OnEvent", "[OnEvent] method parameter must be an IGenEvent type.", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }

                    bool isAsync;
                    var retType = methodSymbol.ReturnType;
                    if (retType.SpecialType == SpecialType.System_Boolean)
                    {
                        isAsync = false;
                    }
                    else if (retType.SpecialType == SpecialType.System_Void)
                    {
                        isAsync = false;
                    }
                    else if (retType is INamedTypeSymbol namedRet)
                    {
                        var orig = namedRet.OriginalDefinition;
                        if (taskSymbol != null && SymbolEqualityComparer.Default.Equals(orig, taskSymbol))
                        {
                            isAsync = true;
                        }
                        else if (taskOfTSymbol != null && SymbolEqualityComparer.Default.Equals(orig, taskOfTSymbol)
                                 && namedRet.TypeArguments.Length == 1
                                 && namedRet.TypeArguments[0].SpecialType == SpecialType.System_Boolean)
                        {
                            isAsync = true;
                        }
                        else
                        {
                            diagnostics.Add(Diagnostic.Create(
                                new DiagnosticDescriptor("GE014", "Invalid OnEvent", "[OnEvent] method return type must be void, bool, Task, or Task<bool>.", "GenEvent", DiagnosticSeverity.Error, true),
                                methodDecl.GetLocation()));
                            continue;
                        }
                    }
                    else
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE014", "Invalid OnEvent", "[OnEvent] method return type must be void, bool, Task, or Task<bool>.", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }

                    var containingType = methodSymbol.ContainingType;
                    if (containingType == null || containingType.TypeKind != TypeKind.Class)
                        continue;

                    if (!eventMethodsByClass.TryGetValue(containingType, out var eventDict))
                        eventMethodsByClass[containingType] = eventDict = new Dictionary<INamedTypeSymbol, List<(IMethodSymbol, Location, bool, SubscriberPriority)>>(SymbolEqualityComparer.Default);

                    if (!eventDict.TryGetValue(paramType, out var list))
                        eventDict[paramType] = list = new List<(IMethodSymbol, Location, bool, SubscriberPriority)>();

                    if (list.Any(t => t.isAsync == isAsync))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE013", "Duplicate OnEvent", "A class can only have one [OnEvent] method per event type (one sync and one async allowed).", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }

                    var priority = SubscriberPriority.Medium;
                    if (onEventAttr.ConstructorArguments.Length > 0 &&
                        onEventAttr.ConstructorArguments[0].Value is int priorityVal)
                    {
                        priority = (SubscriberPriority)priorityVal;
                    }
                    list.Add((methodSymbol, methodDecl.GetLocation(), isAsync, priority));

                    subscribers.Add(new SubscriberInfo
                    {
                        SubscriberType = containingType,
                        Method = methodSymbol,
                        EventType = paramType,
                        Priority = priority,
                        IsAsync = isAsync
                    });
                }
            }

            // Second pass: inject SubscriberInfo entries for derived classes that inherit [OnEvent]
            // handlers from ancestors but don't redeclare [OnEvent] for those event types.
            // This allows GameManager2 (no override) and GameManager3 (override without [OnEvent])
            // to receive events via virtual dispatch, fixing Issue #1.
            var allConcreteClasses = new List<INamedTypeSymbol>();
            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = tree.GetRoot();
                var model = compilation.GetSemanticModel(tree);
                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(classDecl, context.CancellationToken) is INamedTypeSymbol sym
                        && !sym.IsAbstract
                        && sym.TypeKind == TypeKind.Class
                        && sym.TypeParameters.Length == 0)
                    {
                        allConcreteClasses.Add(sym);
                    }
                }
            }

            foreach (var classType in allConcreteClasses)
            {
                // Collect event types already directly handled by this class (own [OnEvent])
                var coveredEvents = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                if (eventMethodsByClass.TryGetValue(classType, out var ownDict))
                {
                    foreach (var et in ownDict.Keys)
                        coveredEvents.Add(et);
                }

                // Walk base class chain; for each event type first found in an ancestor,
                // add an inherited SubscriberInfo for this derived class.
                var baseType = classType.BaseType;
                while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                {
                    if (eventMethodsByClass.TryGetValue(baseType, out var baseDict))
                    {
                        foreach (var kvp in baseDict)
                        {
                            var eventType = kvp.Key;
                            if (!coveredEvents.Add(eventType))
                                continue; // Already handled by this class or a closer ancestor

                            foreach (var (method, _, isAsync, inheritedPriority) in kvp.Value)
                            {
                                subscribers.Add(new SubscriberInfo
                                {
                                    SubscriberType = classType,
                                    Method = method,
                                    EventType = eventType,
                                    Priority = inheritedPriority,
                                    IsAsync = isAsync
                                });
                            }
                        }
                    }
                    baseType = baseType.BaseType;
                }
            }

            return (subscribers, diagnostics);
        }

        private static bool ImplementsIGenEvent(INamedTypeSymbol type, Compilation compilation)
        {
            var iGenEvent = compilation.GetTypeByMetadataName(GenEventMetadataName);
            if (iGenEvent == null) return false;

            var constructed = iGenEvent.Construct(type);
            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iGenEvent));
        }

        private static Dictionary<INamedTypeSymbol, List<SubscriberInfo>> BuildEventSubscriberMap(List<SubscriberInfo> subscribers)
        {
            var map = new Dictionary<INamedTypeSymbol, List<SubscriberInfo>>(SymbolEqualityComparer.Default);
            foreach (var s in subscribers)
            {
                if (!map.TryGetValue(s.EventType, out var list))
                    map[s.EventType] = list = new List<SubscriberInfo>();
                list.Add(s);
            }
            foreach (var list in map.Values)
            {
                list.Sort((a, b) => ((int)a.Priority).CompareTo((int)b.Priority));
            }
            return map;
        }

        private static Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool, bool IsAsync)>> BuildSubscriberEventMap(List<SubscriberInfo> subscribers)
        {
            var map = new Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol, string, bool, bool)>>(SymbolEqualityComparer.Default);
            foreach (var s in subscribers)
            {
                if (!map.TryGetValue(s.SubscriberType, out var list))
                    map[s.SubscriberType] = list = new List<(INamedTypeSymbol, string, bool, bool)>();
                var returnsBool = s.Method.ReturnType.SpecialType == SpecialType.System_Boolean
                    || (s.IsAsync && s.Method.ReturnType is INamedTypeSymbol nt && nt.TypeArguments.Length == 1 && nt.TypeArguments[0].SpecialType == SpecialType.System_Boolean);
                list.Add((s.EventType, s.Method.Name, returnsBool, s.IsAsync));
            }
            return map;
        }

        private static string GenerateEventPublisher(EventInfo evt, IReadOnlyList<SubscriberInfo> subscriberList, string template)
        {
            var usings = CollectUsings(evt.EventType, subscriberList.Select(s => s.SubscriberType));
            var syncSnapshotDeclarations = new StringBuilder();
            var syncSnapshotReturns = new StringBuilder();
            var syncInvocations = new StringBuilder();
            var seenSyncTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var sub in subscriberList)
            {
                if (sub.IsAsync) continue;
                if (seenSyncTypes.Add(sub.SubscriberType))
                {
                    var subscriberTypeName = GetFullyQualifiedTypeName(sub.SubscriberType);
                    var snapshotVariableName = GetSubscriberSnapshotVariableName(sub.SubscriberType);
                    syncSnapshotDeclarations.AppendLine($"        var {snapshotVariableName} = GenEventRegistry<TGenEvent, {subscriberTypeName}>.TakeSubscribersSnapshot();");
                    syncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.ReturnSubscribersSnapshot({snapshotVariableName});");
                    syncInvocations.AppendLine($"            completed = @event.Invoke<{subscriberTypeName}, TGenEvent>(config, {snapshotVariableName});");
                    syncInvocations.AppendLine("        if (!completed) return false;");
                    syncInvocations.AppendLine();
                }
            }

            var asyncSnapshotDeclarations = new StringBuilder();
            var asyncSnapshotReturns = new StringBuilder();
            var asyncInvocations = new StringBuilder();
            var seenAsyncTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var sub in subscriberList)
            {
                if (!seenAsyncTypes.Add(sub.SubscriberType)) continue;
                var subscriberTypeName = GetFullyQualifiedTypeName(sub.SubscriberType);
                var snapshotVariableName = GetSubscriberSnapshotVariableName(sub.SubscriberType);
                asyncSnapshotDeclarations.AppendLine($"        var {snapshotVariableName} = GenEventRegistry<TGenEvent, {subscriberTypeName}>.TakeSubscribersSnapshot();");
                asyncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.ReturnSubscribersSnapshot({snapshotVariableName});");
                asyncInvocations.AppendLine($"            completed = await @event.InvokeAsync<{subscriberTypeName}, TGenEvent>(config, {snapshotVariableName});");
                asyncInvocations.AppendLine("        if (!completed) return false;");
                asyncInvocations.AppendLine();
            }

            return template
                .Replace("{UsingNamespaces}", usings)
                .Replace("{EventClassName}", evt.GeneratedPublisherName)
                .Replace("{EventFullName}", evt.TypeDisplayName)
                .Replace("{SubscriberSnapshots}", syncSnapshotDeclarations.ToString().TrimEnd())
                .Replace("{SubscriberInvocations}", syncInvocations.ToString().TrimEnd())
                .Replace("{SubscriberSnapshotReturns}", syncSnapshotReturns.ToString().TrimEnd())
                .Replace("{SubscriberSnapshotsAsync}", asyncSnapshotDeclarations.ToString().TrimEnd())
                .Replace("{SubscriberInvocationsAsync}", asyncInvocations.ToString().TrimEnd())
                .Replace("{SubscriberSnapshotReturnsAsync}", asyncSnapshotReturns.ToString().TrimEnd());
        }

        private static string GetSubscriberSnapshotVariableName(INamedTypeSymbol subscriberType)
        {
            return "__genEventSnapshot_" + SanitizeIdentifier(subscriberType.ToDisplayString());
        }

        private static string GetGeneratedPublisherName(INamedTypeSymbol eventType)
        {
            return "GenEventPublisher_" + SanitizeIdentifier(GetFullyQualifiedTypeName(eventType));
        }

        private static string GetGeneratedSubscriberRegistryName(INamedTypeSymbol subscriberType)
        {
            return "GenEventSubscriberRegistry_" + SanitizeIdentifier(GetFullyQualifiedTypeName(subscriberType));
        }

        private static string GetFullyQualifiedTypeName(INamedTypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string SanitizeIdentifier(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString();
        }

        private static string GenerateSubscriberRegistry(SubscriberInfo sub,
            IReadOnlyList<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool, bool IsAsync)> subscriberEvents,
            string template)
        {
            var events = subscriberEvents.Count > 0
                ? subscriberEvents
                : new List<(INamedTypeSymbol, string, bool, bool)> { (sub.EventType, sub.Method.Name, sub.Method.ReturnType.SpecialType == SpecialType.System_Boolean, sub.IsAsync) };

            var usings = CollectUsings(sub.SubscriberType, events.Select(e => e.EventType));
            var eventRegistrations = new StringBuilder();
            var startCalls = new StringBuilder();
            var stopCalls = new StringBuilder();
            var seenEventTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var (eventType, methodName, returnsBool, isAsync) in events)
            {
                var eventTypeName = GetFullyQualifiedTypeName(eventType);
                var subscriberTypeName = GetFullyQualifiedTypeName(sub.SubscriberType);
                if (isAsync)
                {
                    var returnExpr = returnsBool
                        ? "return await subscriber." + methodName + "(gameEvent);"
                        : "await subscriber." + methodName + "(gameEvent); return true;";
                    eventRegistrations.AppendLine($"        GenEventRegistry<{eventTypeName}, {subscriberTypeName}>.InitializeAsync(async (gameEvent, subscriber) =>");
                    eventRegistrations.AppendLine("        {");
                    eventRegistrations.AppendLine($"            {returnExpr}");
                    eventRegistrations.AppendLine("        });");
                }
                else
                {
                    var returnExpr = returnsBool ? "return subscriber." + methodName + "(gameEvent);" : "subscriber." + methodName + "(gameEvent); return true;";
                    eventRegistrations.AppendLine($"        GenEventRegistry<{eventTypeName}, {subscriberTypeName}>.Initialize((gameEvent, subscriber) =>");
                    eventRegistrations.AppendLine("        {");
                    eventRegistrations.AppendLine($"            {returnExpr}");
                    eventRegistrations.AppendLine("        });");
                }
                eventRegistrations.AppendLine();

                if (seenEventTypes.Add(eventType))
                {
                    var concreteType = subscriberTypeName;
                    var evtType = eventTypeName;
                    startCalls.AppendLine($"        GenEventRegistry<{evtType}, {concreteType}>.Register(({concreteType})(object)self);");
                    stopCalls.AppendLine($"        GenEventRegistry<{evtType}, {concreteType}>.UnRegister(({concreteType})(object)self);");
                }
            }

            return template
                .Replace("{UsingNamespaces}", usings)
                .Replace("{SubscriberRegistryClassName}", GetGeneratedSubscriberRegistryName(sub.SubscriberType))
                .Replace("{SubscriberFullName}", sub.SubscriberType.ToDisplayString())
                .Replace("{EventRegistrations}", eventRegistrations.ToString().TrimEnd())
                .Replace("{StartListeningCalls}", startCalls.ToString().TrimEnd())
                .Replace("{StopListeningCalls}", stopCalls.ToString().TrimEnd());
        }

        private const string GlobalNamespaceDisplay = "<global namespace>";

        private static string CollectUsings(INamedTypeSymbol primary, IEnumerable<INamedTypeSymbol> additional)
        {
            var namespaces = new HashSet<string> { "GenEvent", "GenEvent.Interface" };
            if (primary != null)
            {
                var ns = primary.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(ns) && ns != GlobalNamespaceDisplay)
                    namespaces.Add(ns);
            }
            foreach (var sym in additional)
            {
                if (sym != null)
                {
                    var ns = sym.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrEmpty(ns) && ns != GlobalNamespaceDisplay)
                        namespaces.Add(ns);
                }
            }
            var usings = namespaces.OrderBy(n => n).Select(n => $"using {n};").ToList();
            return string.Join(Environment.NewLine, usings);
        }

        private struct EventInfo
        {
            public INamedTypeSymbol EventType;
            public string TypeDisplayName;
            public string GeneratedPublisherName;
        }

        private struct SubscriberInfo
        {
            public INamedTypeSymbol SubscriberType { get; set; }
            public IMethodSymbol Method { get; set; }
            public INamedTypeSymbol EventType { get; set; }
            public SubscriberPriority Priority { get; set; }
            public bool IsAsync { get; set; }
        }
    }
}
