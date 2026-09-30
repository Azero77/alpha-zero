using System;
using System.Linq;
using System.Reflection;

public class Program {
    public static void Main() {
        var mt = Assembly.Load("MassTransit");
        var types = mt.GetTypes().Where(t => t.Name.Contains("BusOutboxNotification")).ToList();
        foreach(var t in types) {
            Console.WriteLine(t.FullName);
        }
        var mtef = Assembly.Load("MassTransit.EntityFrameworkCoreIntegration");
        types = mtef.GetTypes().Where(t => t.Name.Contains("BusOutboxNotification")).ToList();
        foreach(var t in types) {
            Console.WriteLine(t.FullName);
        }
    }
}
