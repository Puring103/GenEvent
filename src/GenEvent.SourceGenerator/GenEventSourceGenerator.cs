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

                var events = CollectEvents(compilation, iGenEventSymbol);
                var (subscribers, diagnostics) = CollectSubscribers(compilation, onEventAttributeSymbol, context);

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
                    context.AddSource($"{evt.Name}Publisher.g.cs", SourceText.From(source, Encoding.UTF8));
                }

                foreach (var sub in subscribers.GroupBy(s => s.SubscriberType, SymbolEqualityComparer.Default).Select(g => g.First()))
                {
                    if (!subscriberToEvents.TryGetValue(sub.SubscriberType, out var evtList))
                        evtList = new List<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool)>();
                    var source = GenerateSubscriberRegistry(sub, evtList, subscriberRegistryTemplate);
                    context.AddSource($"{sub.SubscriberType.Name}SubscriberRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
                }

                var publisherRegistrations = string.Join(Environment.NewLine,
                    events.Select(e => $"        BaseEventPublisher.Publishers[typeof({e.FullName})] = new {e.Name}Publisher();"));
                var subscriberRegs = subscribers.GroupBy(s => s.SubscriberType, SymbolEqualityComparer.Default);
                var subscriberRegistrations = string.Join(Environment.NewLine,
                    subscriberRegs.Select(g => $"        BaseSubscriberRegistry.Subscribers[typeof({g.Key.ToDisplayString()})] = new {g.Key.Name}SubscriberRegistry();"));

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

        private static List<EventInfo> CollectEvents(Compilation compilation, INamedTypeSymbol iGenEventSymbol)
        {
            var result = new List<EventInfo>();
            var iGenEventInterface = iGenEventSymbol.ConstructUnboundGenericType();
            var visitor = new EventCollector(compilation, result);
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                visitor.Visit(tree.GetRoot(), model);
            }
            return result;
        }

        private static (List<SubscriberInfo> subscribers, List<Diagnostic> diagnostics) CollectSubscribers(
            Compilation compilation, INamedTypeSymbol onEventAttributeSymbol, GeneratorExecutionContext context)
        {
            var subscribers = new List<SubscriberInfo>();
            var diagnostics = new List<Diagnostic>();
            var eventMethodsByClass = new Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol, (IMethodSymbol method, Location loc)>>(SymbolEqualityComparer.Default);

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

                    var containingType = methodSymbol.ContainingType;
                    if (containingType == null || containingType.TypeKind != TypeKind.Class)
                        continue;

                    if (!eventMethodsByClass.TryGetValue(containingType, out var eventDict))
                        eventMethodsByClass[containingType] = eventDict = new Dictionary<INamedTypeSymbol, (IMethodSymbol, Location)>(SymbolEqualityComparer.Default);

                    if (eventDict.TryGetValue(paramType, out var existing))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor("GE013", "Duplicate OnEvent", "A class can only have one [OnEvent] method per event type.", "GenEvent", DiagnosticSeverity.Error, true),
                            methodDecl.GetLocation()));
                        continue;
                    }
                    eventDict[paramType] = (methodSymbol, methodDecl.GetLocation());

                    var priority = SubscriberPriority.Medium;
                    if (onEventAttr.ConstructorArguments.Length > 0 &&
                        onEventAttr.ConstructorArguments[0].Value is int priorityVal)
                    {
                        priority = (SubscriberPriority)priorityVal;
                    }

                    subscribers.Add(new SubscriberInfo
                    {
                        SubscriberType = containingType,
                        Method = methodSymbol,
                        EventType = paramType,
                        Priority = priority
                    });
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

        private static Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool)>> BuildSubscriberEventMap(List<SubscriberInfo> subscribers)
        {
            var map = new Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol, string, bool)>>(SymbolEqualityComparer.Default);
            foreach (var s in subscribers)
            {
                if (!map.TryGetValue(s.SubscriberType, out var list))
                    map[s.SubscriberType] = list = new List<(INamedTypeSymbol, string, bool)>();
                list.Add((s.EventType, s.Method.Name, s.Method.ReturnType.SpecialType == SpecialType.System_Boolean));
            }
            return map;
        }

        private static string GenerateEventPublisher(EventInfo evt, IReadOnlyList<SubscriberInfo> subscriberList, string template)
        {
            var usings = CollectUsings(evt.EventType, subscriberList.Select(s => s.SubscriberType));
            var invocations = new StringBuilder();
            foreach (var sub in subscriberList)
            {
                invocations.AppendLine($"        completed = @event.Invoke<{sub.SubscriberType.ToDisplayString()}, TGenEvent>(config);");
                invocations.AppendLine("        if (!completed) return false;");
                invocations.AppendLine();
            }

            return template
                .Replace("{UsingNamespaces}", usings)
                .Replace("{EventName}", evt.Name)
                .Replace("{EventFullName}", evt.FullName)
                .Replace("{SubscriberInvocations}", invocations.ToString().TrimEnd());
        }

        private static string GenerateSubscriberRegistry(SubscriberInfo sub,
            IReadOnlyList<(INamedTypeSymbol EventType, string MethodName, bool ReturnsBool)> subscriberEvents,
            string template)
        {
            var events = subscriberEvents.Count > 0
                ? subscriberEvents
                : new List<(INamedTypeSymbol, string, bool)> { (sub.EventType, sub.Method.Name, sub.Method.ReturnType.SpecialType == SpecialType.System_Boolean) };

            var usings = CollectUsings(sub.SubscriberType, events.Select(e => e.EventType));
            var eventRegistrations = new StringBuilder();
            var startCalls = new StringBuilder();
            var stopCalls = new StringBuilder();

            foreach (var (eventType, methodName, returnsBool) in events)
            {
                var returnExpr = returnsBool ? "return subscriber." + methodName + "(gameEvent);" : "subscriber." + methodName + "(gameEvent); return true;";
                eventRegistrations.AppendLine($"        GenEventRegistry<{eventType.ToDisplayString()}, {sub.SubscriberType.ToDisplayString()}>.Initialize((gameEvent, subscriber) =>");
                eventRegistrations.AppendLine("        {");
                eventRegistrations.AppendLine($"            {returnExpr}");
                eventRegistrations.AppendLine("        });");
                eventRegistrations.AppendLine();

                startCalls.AppendLine($"        StartListening<TSubscriber, {eventType.ToDisplayString()}>(self);");
                stopCalls.AppendLine($"        StopListening<TSubscriber, {eventType.ToDisplayString()}>(self);");
            }

            return template
                .Replace("{UsingNamespaces}", usings)
                .Replace("{SubscriberName}", sub.SubscriberType.Name)
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
            public string Name;
            public string FullName;
        }

        private struct SubscriberInfo
        {
            public INamedTypeSymbol SubscriberType { get; set; }
            public IMethodSymbol Method { get; set; }
            public INamedTypeSymbol EventType { get; set; }
            public SubscriberPriority Priority { get; set; }
        }

        private class EventCollector : CSharpSyntaxWalker
        {
            private readonly Compilation _compilation;
            private readonly List<EventInfo> _result;
            private SemanticModel _model = null!;

            public EventCollector(Compilation compilation, List<EventInfo> result)
            {
                _compilation = compilation;
                _result = result;
            }

            public void Visit(SyntaxNode node, SemanticModel model)
            {
                _model = model;
                Visit(node);
            }

            public override void VisitStructDeclaration(StructDeclarationSyntax node)
            {
                var symbol = _model.GetDeclaredSymbol(node, default);
                if (symbol is INamedTypeSymbol named && ImplementsIGenEvent(named, _compilation))
                {
                    _result.Add(new EventInfo
                    {
                        EventType = named,
                        Name = named.Name,
                        FullName = named.ToDisplayString()
                    });
                }
                base.VisitStructDeclaration(node);
            }

            private static bool ImplementsIGenEvent(INamedTypeSymbol type, Compilation compilation)
            {
                var iGenEvent = compilation.GetTypeByMetadataName(GenEventMetadataName);
                if (iGenEvent == null) return false;
                return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iGenEvent));
            }
        }
    }
}
