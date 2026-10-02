using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        var p1 = Assembly.LoadFrom("/home/azero/.nuget/packages/masstransit.entityframeworkcore/8.5.8/lib/net8.0/MassTransit.EntityFrameworkCoreIntegration.dll");
        var p2 = Assembly.LoadFrom("/home/azero/.nuget/packages/masstransit/8.5.8/lib/net8.0/MassTransit.dll");
        
        foreach (var t in p1.GetTypes().Where(t => t.Name.Contains("InboxCleanupService"))) {
            Console.WriteLine(t.FullName);
            foreach (var c in t.GetConstructors()) {
                Console.WriteLine("  " + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)));
            }
        }
        foreach (var t in p1.GetTypes().Where(t => t.Name.Contains("BusOutboxDeliveryService"))) {
            Console.WriteLine(t.FullName);
            foreach (var c in t.GetConstructors()) {
                Console.WriteLine("  " + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)));
            }
        }
        foreach (var t in p2.GetTypes().Where(t => t.Name.Contains("ScopedBusContext"))) {
            Console.WriteLine(t.FullName);
        }
    }
}
