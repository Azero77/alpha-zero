using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MassTransit;

public class AppDbContext {}

public static class P 
{
    public static void Main()
    {
        Console.WriteLine(KebabCaseEndpointNameFormatter.Instance.Consumer<P>());
    }
}
