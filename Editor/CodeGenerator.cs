using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using GenEvent.Runtime;

namespace GenEvent.Editor
{
    public class CodeGenerator
    {
        private readonly List<string> _errors = new List<string>();
        private readonly Dictionary<Type, EventInfo> _events = new Dictionary<Type, EventInfo>();
        private readonly Dictionary<Type, List<SubscriberInfo>> _subscribers = new Dictionary<Type, List<SubscriberInfo>>();

        public bool GenerateCode(string outputPath)
        {
            _errors.Clear();
            _events.Clear();
            _subscribers.Clear();

            // 收集事件和订阅者
            CollectEvents();
            CollectSubscribers();

            // 验证
            if (!Validate())
            {
                return false;
            }

            // 生成代码
            GeneratePublisherFiles(outputPath);
            GenerateSubscriberRegistryFiles(outputPath);
            GenerateEventPublishersFile(outputPath);
            GenerateSubscriberRegistrysFile(outputPath);

            AssetDatabase.Refresh();
            return true;
        }

        private void CollectEvents()
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var iGenEventType = typeof(IGenEvent<>);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        // 只处理结构体
                        if (!type.IsValueType || type.IsEnum)
                            continue;

                        // 排除泛型定义
                        if (type.IsGenericTypeDefinition)
                            continue;

                        // 检查是否实现IGenEvent<T>
                        if (ImplementsIGenEvent(type, iGenEventType))
                        {
                            _events[type] = new EventInfo
                            {
                                EventType = type,
                                EventName = type.Name,
                                Subscribers = new List<SubscriberInfo>()
                            };
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 忽略无法加载的类型
                    Debug.LogWarning($"无法加载程序集 {assembly.FullName} 中的某些类型: {ex.Message}");
                }
            }
        }

        private bool ImplementsIGenEvent(Type type, Type iGenEventType)
        {
            var interfaces = type.GetInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == iGenEventType)
                {
                    return true;
                }
            }
            return false;
        }

        private void CollectSubscribers()
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var onEventAttributeType = typeof(OnEventAttribute);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        // 只处理类
                        if (type.IsInterface || type.IsAbstract || type.IsValueType)
                            continue;

                        // 排除泛型定义
                        if (type.IsGenericTypeDefinition)
                            continue;

                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        foreach (var method in methods)
                        {
                            var attribute = method.GetCustomAttribute(onEventAttributeType);
                            if (attribute != null)
                            {
                                // 验证方法必须是public（BindingFlags.Public已经确保，但显式检查更清晰）
                                if (!method.IsPublic)
                                {
                                    _errors.Add($"方法 {type.FullName}.{method.Name} 必须为 public");
                                    continue;
                                }

                                var onEventAttr = (OnEventAttribute)attribute;
                                var parameters = method.GetParameters();

                                // 验证方法签名
                                if (parameters.Length != 1)
                                {
                                    _errors.Add($"方法 {type.FullName}.{method.Name} 的 [OnEvent] 属性要求方法必须只有一个参数");
                                    continue;
                                }

                                var eventParamType = parameters[0].ParameterType;
                                if (!ImplementsIGenEvent(eventParamType, typeof(IGenEvent<>)))
                                {
                                    _errors.Add($"方法 {type.FullName}.{method.Name} 的参数类型 {eventParamType.Name} 必须实现 IGenEvent<T>");
                                    continue;
                                }

                                var returnType = method.ReturnType;
                                bool returnsBool = returnType == typeof(bool);
                                if (!returnsBool && returnType != typeof(void))
                                {
                                    _errors.Add($"方法 {type.FullName}.{method.Name} 的返回类型必须是 void 或 bool");
                                    continue;
                                }

                                // 检查一个类对同一事件类型是否已经有订阅方法
                                if (_subscribers.TryGetValue(type, out var existingSubscribers))
                                {
                                    if (existingSubscribers.Any(s => s.EventType == eventParamType))
                                    {
                                        var existing = existingSubscribers.First(s => s.EventType == eventParamType);
                                        _errors.Add($"类 {type.FullName} 对事件类型 {eventParamType.Name} 已经有订阅方法 {existing.MethodName}，不能有多个");
                                        continue;
                                    }
                                }

                                var subscriberInfo = new SubscriberInfo
                                {
                                    SubscriberType = type,
                                    SubscriberName = type.Name,
                                    Method = method,
                                    EventType = eventParamType,
                                    Priority = onEventAttr.Priority,
                                    MethodName = method.Name,
                                    ReturnsBool = returnsBool
                                };

                                if (!_subscribers.ContainsKey(type))
                                {
                                    _subscribers[type] = new List<SubscriberInfo>();
                                }
                                _subscribers[type].Add(subscriberInfo);

                                // 添加到对应事件的订阅者列表
                                if (_events.TryGetValue(eventParamType, out var eventInfo))
                                {
                                    eventInfo.Subscribers.Add(subscriberInfo);
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"无法加载程序集 {assembly.FullName} 中的某些类型: {ex.Message}");
                }
            }

            // 按优先级排序订阅者
            foreach (var eventInfo in _events.Values)
            {
                eventInfo.Subscribers.Sort((a, b) =>
                {
                    var priorityCompare = a.Priority.CompareTo(b.Priority);
                    if (priorityCompare != 0)
                        return priorityCompare;
                    // 相同优先级按类型名称排序
                    return string.Compare(a.SubscriberType.Name, b.SubscriberType.Name, StringComparison.Ordinal);
                });
            }
        }

        private bool Validate()
        {
            if (_errors.Count > 0)
            {
                Debug.LogError("代码生成失败，发现以下错误：");
                foreach (var error in _errors)
                {
                    Debug.LogError(error);
                }
                return false;
            }
            return true;
        }

        private void GeneratePublisherFiles(string outputPath)
        {
            foreach (var eventInfo in _events.Values)
            {
                if (eventInfo.Subscribers.Count == 0)
                    continue;

                var template = LoadTemplate("PublisherTemplate.txt");
                var usings = GenerateUsings(eventInfo.Subscribers);
                var invokeCalls = GenerateInvokeCalls(eventInfo.Subscribers);

                var code = template
                    .Replace("{USINGS}", usings)
                    .Replace("{EVENT_NAME}", eventInfo.EventName)
                    .Replace("{INVOKE_CALLS}", invokeCalls);

                var fileName = $"{eventInfo.EventName}Publisher.g.cs";
                WriteFile(outputPath, fileName, code);
            }
        }

        private void GenerateSubscriberRegistryFiles(string outputPath)
        {
            foreach (var kvp in _subscribers)
            {
                var subscriberType = kvp.Key;
                var subscriberInfos = kvp.Value;

                var template = LoadTemplate("SubscriberRegistryTemplate.txt");
                var usings = GenerateUsings(subscriberInfos);
                var initializations = GenerateInitializations(subscriberInfos);
                var startListeningCalls = GenerateStartListeningCalls(subscriberInfos);
                var stopListeningCalls = GenerateStopListeningCalls(subscriberInfos);

                var code = template
                    .Replace("{USINGS}", usings)
                    .Replace("{SUBSCRIBER_NAME}", subscriberType.Name)
                    .Replace("{INITIALIZATIONS}", initializations)
                    .Replace("{START_LISTENING_CALLS}", startListeningCalls)
                    .Replace("{STOP_LISTENING_CALLS}", stopListeningCalls);

                var fileName = $"{subscriberType.Name}SubscriberRegistry.g.cs";
                WriteFile(outputPath, fileName, code);
            }
        }

        private void GenerateEventPublishersFile(string outputPath)
        {
            var eventsWithSubscribers = _events.Values.Where(e => e.Subscribers.Count > 0).ToList();
            if (eventsWithSubscribers.Count == 0)
                return;

            var template = LoadTemplate("EventPublishersTemplate.txt");
            var namespaceUsings = GenerateNamespaceUsings(eventsWithSubscribers.Select(e => e.EventType));
            var registrations = GenerateEventPublisherRegistrations(eventsWithSubscribers);

            var code = template
                .Replace("{NAMESPACE_USINGS}", namespaceUsings)
                .Replace("{REGISTRATIONS}", registrations);

            WriteFile(outputPath, "EventPublishers.g.cs", code);
        }

        private void GenerateSubscriberRegistrysFile(string outputPath)
        {
            if (_subscribers.Count == 0)
                return;

            var template = LoadTemplate("SubscriberRegistrysTemplate.txt");
            var allSubscriberInfos = _subscribers.Values.SelectMany(list => list).ToList();
            var namespaceUsings = GenerateNamespaceUsings(allSubscriberInfos.Select(s => s.SubscriberType));
            var registrations = GenerateSubscriberRegistryRegistrations(allSubscriberInfos);

            var code = template
                .Replace("{NAMESPACE_USINGS}", namespaceUsings)
                .Replace("{REGISTRATIONS}", registrations);

            WriteFile(outputPath, "SubscriberRegistrys.g.cs", code);
        }

        private string GenerateUsings(List<SubscriberInfo> subscribers)
        {
            var usings = new HashSet<string> { "using GenEvent.Runtime;" };
            foreach (var subscriber in subscribers)
            {
                if (!string.IsNullOrEmpty(subscriber.SubscriberType.Namespace))
                {
                    usings.Add($"using {subscriber.SubscriberType.Namespace};");
                }
                if (!string.IsNullOrEmpty(subscriber.EventType.Namespace))
                {
                    usings.Add($"using {subscriber.EventType.Namespace};");
                }
            }
            return string.Join("\n", usings.OrderBy(u => u));
        }

        private string GenerateInvokeCalls(List<SubscriberInfo> subscribers)
        {
            var sb = new StringBuilder();
            foreach (var subscriber in subscribers)
            {
                sb.AppendLine($"        completed = @event.Invoke<{subscriber.SubscriberType.Name}, TGenEvent>();");
                sb.AppendLine("        if (!completed) return false;");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private string GenerateInitializations(List<SubscriberInfo> subscribers)
        {
            var sb = new StringBuilder();
            foreach (var subscriber in subscribers)
            {
                sb.AppendLine($"        GenEventRegistry<{subscriber.EventType.Name}, {subscriber.SubscriberType.Name}>.Initialize((gameEvent, subscriber) =>");
                sb.AppendLine("        {");
                if (subscriber.ReturnsBool)
                {
                    sb.AppendLine($"            return subscriber.{subscriber.MethodName}(gameEvent);");
                }
                else
                {
                    sb.AppendLine($"            subscriber.{subscriber.MethodName}(gameEvent);");
                    sb.AppendLine("            return true;");
                }
                sb.AppendLine("        });");
                if (subscriber != subscribers.Last())
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private string GenerateStartListeningCalls(List<SubscriberInfo> subscribers)
        {
            var sb = new StringBuilder();
            foreach (var subscriber in subscribers)
            {
                sb.AppendLine($"        StartListening<TSubscriber, {subscriber.EventType.Name}>(self);");
            }
            return sb.ToString();
        }

        private string GenerateStopListeningCalls(List<SubscriberInfo> subscribers)
        {
            var sb = new StringBuilder();
            foreach (var subscriber in subscribers)
            {
                sb.AppendLine($"        StopListening<TSubscriber, {subscriber.EventType.Name}>(self);");
            }
            return sb.ToString();
        }

        private string GenerateNamespaceUsings(IEnumerable<Type> types)
        {
            var usings = new HashSet<string>();
            foreach (var type in types)
            {
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    usings.Add($"using {type.Namespace};");
                }
            }
            return string.Join("\n", usings.OrderBy(u => u));
        }

        private string GenerateEventPublisherRegistrations(IEnumerable<EventInfo> events)
        {
            var sb = new StringBuilder();
            foreach (var eventInfo in events)
            {
                if (eventInfo.Subscribers.Count == 0)
                    continue;
                sb.AppendLine($"        Publishers[typeof({eventInfo.EventName})] = new {eventInfo.EventName}Publisher();");
            }
            return sb.ToString();
        }

        private string GenerateSubscriberRegistryRegistrations(IEnumerable<SubscriberInfo> subscribers)
        {
            var sb = new StringBuilder();
            var subscriberTypes = subscribers.Select(s => s.SubscriberType).Distinct().ToList();
            foreach (var subscriberType in subscriberTypes)
            {
                sb.AppendLine($"        Subscribers[typeof({subscriberType.Name})] = new {subscriberType.Name}SubscriberRegistry();");
            }
            return sb.ToString();
        }

        private string LoadTemplate(string templateName)
        {
            return templateName switch
            {
                "PublisherTemplate.txt" => CodeGeneratorTemplates.PublisherTemplate,
                "SubscriberRegistryTemplate.txt" => CodeGeneratorTemplates.SubscriberRegistryTemplate,
                "EventPublishersTemplate.txt" => CodeGeneratorTemplates.EventPublishersTemplate,
                "SubscriberRegistrysTemplate.txt" => CodeGeneratorTemplates.SubscriberRegistrysTemplate,
                _ => throw new System.ArgumentException($"未知的模板名称: {templateName}")
            };
        }

        private void WriteFile(string outputPath, string fileName, string content)
        {
            // 如果输出路径是Assets相对路径，转换为绝对路径
            string fullPath;
            if (outputPath.StartsWith("Assets"))
            {
                fullPath = Path.Combine(Application.dataPath, outputPath.Substring("Assets".Length).TrimStart('/', '\\'), fileName);
            }
            else
            {
                fullPath = Path.Combine(outputPath, fileName);
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, content, Encoding.UTF8);
            Debug.Log($"生成文件: {fullPath}");
        }

        private class EventInfo
        {
            public Type EventType;
            public string EventName;
            public List<SubscriberInfo> Subscribers;
        }

        private class SubscriberInfo
        {
            public Type SubscriberType;
            public string SubscriberName;
            public MethodInfo Method;
            public Type EventType;
            public SubscriberPriority Priority;
            public string MethodName;
            public bool ReturnsBool;
        }
    }
}

