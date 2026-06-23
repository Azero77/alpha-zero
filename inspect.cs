using System;
using System.Linq;
using System.Reflection;
using FastEndpoints;

class Program {
    static void Main() {
        var type = typeof(IPreProcessorContext);
        foreach(var prop in type.GetProperties()) Console.WriteLine("Prop: " + prop.Name);
        foreach(var method in type.GetMethods()) Console.WriteLine("Method: " + method.Name);
    }
}
