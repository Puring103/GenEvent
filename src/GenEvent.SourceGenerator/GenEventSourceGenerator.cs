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
    [Generator]
    public sealed class GenEventSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new OnEventSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not OnEventSyntaxReceiver receiver)
            {
                return;
            }

            var compilation = context.Compilation;

            // locate core GenEvent types
            var onEventAttributeSymbol = compilation.GetTypeByMetadataName("GenEvent.OnEventAttribute");
            var iGenEventSymbol = compilation.GetTypeByMetadataName("GenEvent.Interface.IGenEvent`1");
            var subscriberPrioritySymbol = compilation.GetTypeByMetadataName("GenEvent.SubscriberPriority");

            if (onEventAttributeSymbol is null ||
                iGenEventSymbol is null ||
                subscriberPrioritySymbol is null)
            {
                // Without core types, we cannot generate anything meaningful.
                return;
            }

            var priorityValues = BuildPriorityMap(subscriberPrioritySymbol);

            // event type -> EventInfo
            var events = new Dictionary<INamedTypeSymbol, EventInfo>(SymbolEqualityComparer.Default);
            // subscriber type -> SubscriberInfo
            var subscribers = new Dictionary<INamedTypeSymbol, SubscriberInfo>(SymbolEqualityComparer.Default);

            var semanticModelCache = new Dictionary<SyntaxTree, SemanticModel>();
            var iGenEventOriginal = iGenEventSymbol;

            var index = 0;

            foreach (var methodDecl in receiver.CandidateMethods)
            {
                var tree = methodDecl.SyntaxTree;
                if (!semanticModelCache.TryGetValue(tree, out var model))
                {
                    model = compilation.GetSemanticModel(tree);
                    semanticModelCache[tree] = model;
                }

                if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
                    continue;

                var onEventData = methodSymbol.GetAttributes()
                    .FirstOrDefault(a =>
                        SymbolEqualityComparer.Default.Equals(a.AttributeClass, onEventAttributeSymbol));

                if (onEventData is null)
                    continue;

                AnalyzeOnEventMethod(
                    context,
                    methodDecl,
                    methodSymbol,
                    onEventData,
                    iGenEventOriginal,
                    priorityValues,
                    events,
                    subscribers,
                    ref index);
            }

            if (events.Count == 0)
            {
                return;
            }

            GeneratePublisherSources(context, events);
            GenerateSubscriberRegistrySources(context, subscribers);
            GenerateBaseEventPublisherInitialization(context, events);
        }

        #region Diagnostics

        private const string DiagnosticCategory = "GenEvent";

        private static readonly DiagnosticDescriptor GE001_OnEventMustBePublic = new(
            id: "GE001",
            title: "OnEvent method must be public instance method",
            messageFormat: "[OnEvent] can only be applied to public instance methods.",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "OnEvent methods must be public instance methods.");

        private static readonly DiagnosticDescriptor GE002_OnEventMustHaveSingleParameter = new(
            id: "GE002",
            title: "OnEvent method must have exactly one parameter",
            messageFormat: "[OnEvent] method must have exactly one parameter.",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "OnEvent methods must declare exactly one parameter representing the event type.");

        private static readonly DiagnosticDescriptor GE003_ParameterMustBeIGenEvent = new(
            id: "GE003",
            title: "OnEvent parameter type must be an IGenEvent struct",
            messageFormat: "The parameter type of [OnEvent] must be a struct implementing IGenEvent<TSelf>.",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "OnEvent parameter type must be a struct implementing IGenEvent<TSelf>.");

        private static readonly DiagnosticDescriptor GE004_DuplicateOnEventInSameSubscriber = new(
            id: "GE004",
            title: "Duplicate OnEvent for same event in subscriber",
            messageFormat: "A subscriber class cannot contain more than one [OnEvent] method for the same event type.",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A subscriber class cannot contain more than one [OnEvent] method for the same event type.");

        private static readonly DiagnosticDescriptor GE005_UnsupportedReturnType = new(
            id: "GE005",
            title: "Unsupported OnEvent return type",
            messageFormat: "[OnEvent] methods must return void or bool.",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "OnEvent methods must return void or bool.");

        #endregion

        #region Model

        private sealed class HandlerInfo
        {
            public HandlerInfo(IMethodSymbol method, int priority, int order)
            {
                Method = method;
                Priority = priority;
                Order = order;
                ReturnsBool = !method.ReturnsVoid &&
                              method.ReturnType.SpecialType == SpecialType.System_Boolean;
            }

            public IMethodSymbol Method { get; }
            public int Priority { get; }
            public int Order { get; }
            public bool ReturnsBool { get; }
        }

        private sealed class SubscriberInfo
        {
            public SubscriberInfo(INamedTypeSymbol symbol)
            {
                Symbol = symbol;
            }

            public INamedTypeSymbol Symbol { get; }

            // event type -> handler
            public Dictionary<INamedTypeSymbol, HandlerInfo> HandlersByEvent { get; } =
                new(SymbolEqualityComparer.Default);
        }

        private sealed class EventInfo
        {
            public EventInfo(INamedTypeSymbol symbol)
            {
                Symbol = symbol;
            }

            public INamedTypeSymbol Symbol { get; }

            // all (subscriber, handler) for this event
            public List<(SubscriberInfo Subscriber, HandlerInfo Handler)> Handlers { get; } = new();
        }

        #endregion

        #region Analysis

        private static Dictionary<string, int> BuildPriorityMap(INamedTypeSymbol subscriberPrioritySymbol)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var member in subscriberPrioritySymbol.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.HasConstantValue || member.ConstantValue is not int value)
                    continue;

                map[member.Name] = value;
            }

            return map;
        }

        private static void AnalyzeOnEventMethod(
            GeneratorExecutionContext context,
            MethodDeclarationSyntax methodDecl,
            IMethodSymbol methodSymbol,
            AttributeData onEventData,
            INamedTypeSymbol iGenEventSymbol,
            Dictionary<string, int> priorityValues,
            Dictionary<INamedTypeSymbol, EventInfo> events,
            Dictionary<INamedTypeSymbol, SubscriberInfo> subscribers,
            ref int index)
        {
            // Must be instance, public
            if (methodSymbol.IsStatic || methodSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GE001_OnEventMustBePublic,
                    methodDecl.Identifier.GetLocation()));
                return;
            }

            // Exactly one parameter
            if (methodSymbol.Parameters.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GE002_OnEventMustHaveSingleParameter,
                    methodDecl.Identifier.GetLocation()));
                return;
            }

            var param = methodSymbol.Parameters[0];
            var eventType = param.Type as INamedTypeSymbol;

            if (!IsValidEventType(eventType, iGenEventSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GE003_ParameterMustBeIGenEvent,
                    methodDecl.ParameterList.Parameters[0].GetLocation()));
                return;
            }

            // Return type must be void or bool
            if (!methodSymbol.ReturnsVoid &&
                methodSymbol.ReturnType.SpecialType != SpecialType.System_Boolean)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GE005_UnsupportedReturnType,
                    methodDecl.ReturnType.GetLocation()));
                return;
            }

            // Subscriber type
            if (methodSymbol.ContainingType is not INamedTypeSymbol subscriberType)
            {
                return;
            }

            if (!subscribers.TryGetValue(subscriberType, out var subscriberInfo))
            {
                subscriberInfo = new SubscriberInfo(subscriberType);
                subscribers[subscriberType] = subscriberInfo;
            }

            // One OnEvent per (subscriber, event)
            if (subscriberInfo.HandlersByEvent.ContainsKey(eventType!))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GE004_DuplicateOnEventInSameSubscriber,
                    methodDecl.Identifier.GetLocation()));
                return;
            }

            var priority = GetPriorityValue(onEventData, priorityValues);

            var handlerInfo = new HandlerInfo(methodSymbol, priority, index++);

            subscriberInfo.HandlersByEvent[eventType!] = handlerInfo;

            if (!events.TryGetValue(eventType!, out var eventInfo))
            {
                eventInfo = new EventInfo(eventType!);
                events[eventType!] = eventInfo;
            }

            eventInfo.Handlers.Add((subscriberInfo, handlerInfo));
        }

        private static bool IsValidEventType(INamedTypeSymbol? eventType, INamedTypeSymbol iGenEventSymbol)
        {
            if (eventType is null)
                return false;

            if (eventType.TypeKind != TypeKind.Struct)
                return false;

            foreach (var iface in eventType.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, iGenEventSymbol))
                    continue;

                if (iface.TypeArguments.Length != 1)
                    continue;

                if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], eventType))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetPriorityValue(AttributeData attributeData, Dictionary<string, int> priorityValues)
        {
            if (attributeData.ConstructorArguments.Length == 1 &&
                attributeData.ConstructorArguments[0].Value is int v)
            {
                return v;
            }

            // Fallback to Medium if available
            if (priorityValues.TryGetValue("Medium", out var medium))
            {
                return medium;
            }

            return 0;
        }

        #endregion

        #region Generation - Publishers

        private static void GeneratePublisherSources(
            GeneratorExecutionContext context,
            Dictionary<INamedTypeSymbol, EventInfo> events)
        {
            foreach (var kvp in events)
            {
                var eventSymbol = kvp.Key;
                var eventInfo = kvp.Value;

                var orderedHandlers = eventInfo.Handlers
                    .OrderBy(h => h.Handler.Priority)
                    .ThenBy(h => h.Handler.Order)
                    .ToList();

                var eventFullName = eventSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var publisherClassName = eventSymbol.Name + "Publisher";
                var @namespace = "GenEvent.Generated";

                var sb = new StringBuilder();

                sb.AppendLine("// <auto-generated />");
                sb.AppendLine("// This file is generated by GenEventSourceGenerator. Do not edit manually.");
                sb.AppendLine("using System;");
                sb.AppendLine("using GenEvent;");
                sb.AppendLine("using GenEvent.Interface;");
                sb.AppendLine();
                sb.AppendLine($"namespace {@namespace}");
                sb.AppendLine("{");
                sb.AppendLine($"    internal sealed class {publisherClassName} : BaseEventPublisher");
                sb.AppendLine("    {");
                sb.AppendLine("        public override bool Publish<TGenEvent>(TGenEvent @event, object emitter)");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (typeof(TGenEvent) != typeof({eventFullName}))");
                sb.AppendLine("            {");
                sb.AppendLine("                return true;");
                sb.AppendLine("            }");
                sb.AppendLine();
                sb.AppendLine($"            var gameEvent = ({eventFullName})(object)@event;");
                sb.AppendLine($"            var cancelable = PublishConfig<{eventFullName}>.Instance.Cancelable;");
                sb.AppendLine();

                if (orderedHandlers.Count == 0)
                {
                    sb.AppendLine("            return true;");
                }
                else
                {
                    sb.AppendLine("            if (cancelable)");
                    sb.AppendLine("            {");
                    foreach (var (subscriber, _) in orderedHandlers)
                    {
                        var subscriberFullName =
                            subscriber.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        sb.AppendLine(
                            $"                if (!gameEvent.Invoke<{subscriberFullName}, {eventFullName}>())");
                        sb.AppendLine("                {");
                        sb.AppendLine("                    return false;");
                        sb.AppendLine("                }");
                    }

                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    foreach (var (subscriber, _) in orderedHandlers)
                    {
                        var subscriberFullName =
                            subscriber.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        sb.AppendLine($"                gameEvent.Invoke<{subscriberFullName}, {eventFullName}>();");
                    }

                    sb.AppendLine("            }");
                    sb.AppendLine();
                    sb.AppendLine("            return true;");
                }

                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                var hintName = SanitizeFileName(publisherClassName) + ".g.cs";
                context.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }

        #endregion

        #region Generation - Subscriber Registries

        private static void GenerateSubscriberRegistrySources(
            GeneratorExecutionContext context,
            Dictionary<INamedTypeSymbol, SubscriberInfo> subscribers)
        {
            foreach (var kvp in subscribers)
            {
                var subscriberInfo = kvp.Value;

                if (subscriberInfo.HandlersByEvent.Count == 0)
                    continue;

                var subscriberSymbol = subscriberInfo.Symbol;
                var subscriberFullName = subscriberSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var registryClassName = subscriberSymbol.Name + "SubscriberRegistry";
                var @namespace = "GenEvent.Generated";

                var sb = new StringBuilder();

                sb.AppendLine("// <auto-generated />");
                sb.AppendLine("// This file is generated by GenEventSourceGenerator. Do not edit manually.");
                sb.AppendLine("using System;");
                sb.AppendLine("using GenEvent;");
                sb.AppendLine("using GenEvent.Interface;");
                sb.AppendLine();
                sb.AppendLine($"namespace {@namespace}");
                sb.AppendLine("{");
                sb.AppendLine($"    internal sealed class {registryClassName} : BaseSubscriberRegistry");
                sb.AppendLine("    {");
                sb.AppendLine("        public override void StartListening<TSubscriber>(TSubscriber self)");
                sb.AppendLine("            where TSubscriber : class");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (self is not {subscriberFullName} typed)");
                sb.AppendLine("            {");
                sb.AppendLine("                return;");
                sb.AppendLine("            }");

                foreach (var eventKvp in subscriberInfo.HandlersByEvent)
                {
                    var eventFullName = eventKvp.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    sb.AppendLine(
                        $"            BaseSubscriberRegistry.StartListening<{subscriberFullName}, {eventFullName}>(typed);");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        public override void StopListening<TSubscriber>(TSubscriber self)");
                sb.AppendLine("            where TSubscriber : class");
                sb.AppendLine("        {");
                sb.AppendLine($"            if (self is not {subscriberFullName} typed)");
                sb.AppendLine("            {");
                sb.AppendLine("                return;");
                sb.AppendLine("            }");

                foreach (var eventKvp in subscriberInfo.HandlersByEvent)
                {
                    var eventFullName = eventKvp.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    sb.AppendLine(
                        $"            BaseSubscriberRegistry.StopListening<{subscriberFullName}, {eventFullName}>(typed);");
                }

                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");

                var hintName = SanitizeFileName(registryClassName) + ".g.cs";
                context.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }

        #endregion

        #region Generation - BaseEventPublisher partial init

        private static void GenerateBaseEventPublisherInitialization(
            GeneratorExecutionContext context,
            Dictionary<INamedTypeSymbol, EventInfo> events)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// This file is generated by GenEventSourceGenerator. Do not edit manually.");
            sb.AppendLine("using System;");
            sb.AppendLine("using GenEvent;");
            sb.AppendLine("using GenEvent.Interface;");
            sb.AppendLine();
            sb.AppendLine("namespace GenEvent.Interface");
            sb.AppendLine("{");
            sb.AppendLine("    public abstract partial class BaseEventPublisher");
            sb.AppendLine("    {");
            sb.AppendLine("        static BaseEventPublisher()");
            sb.AppendLine("        {");

            // Register publishers
            foreach (var kvp in events)
            {
                var eventSymbol = kvp.Key;
                var eventFullName = eventSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var publisherClassName = eventSymbol.Name + "Publisher";

                sb.AppendLine(
                    $"            Publishers[typeof({eventFullName})] = new GenEvent.Generated.{publisherClassName}();");
            }

            sb.AppendLine();

            // Initialize GenEventRegistry delegates
            foreach (var kvp in events)
            {
                var eventSymbol = kvp.Key;
                var eventInfo = kvp.Value;
                var eventFullName = eventSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (var (subscriber, handler) in eventInfo.Handlers
                             .OrderBy(h => h.Handler.Priority)
                             .ThenBy(h => h.Handler.Order))
                {
                    var subscriberFullName =
                        subscriber.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var methodName = handler.Method.Name;

                    sb.AppendLine(
                        $"            GenEventRegistry<{eventFullName}, {subscriberFullName}>.Initialize((ev, sub) =>");
                    sb.AppendLine("            {");
                    sb.AppendLine("                bool continuePropagation = true;");

                    if (handler.ReturnsBool)
                    {
                        sb.AppendLine("                if (continuePropagation)");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    continuePropagation = sub.{methodName}(ev);");
                        sb.AppendLine("                }");
                    }
                    else
                    {
                        sb.AppendLine("                if (continuePropagation)");
                        sb.AppendLine("                {");
                        sb.AppendLine($"                    sub.{methodName}(ev);");
                        sb.AppendLine("                }");
                    }

                    sb.AppendLine("                return continuePropagation;");
                    sb.AppendLine("            });");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("BaseEventPublisher.Init.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        #endregion

        #region Helpers

        private static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private sealed class OnEventSyntaxReceiver : ISyntaxReceiver
        {
            public List<MethodDeclarationSyntax> CandidateMethods { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is MethodDeclarationSyntax method &&
                    method.AttributeLists.Count > 0)
                {
                    CandidateMethods.Add(method);
                }
            }
        }

        #endregion
    }
}