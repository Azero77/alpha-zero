using System;
using Microsoft.Extensions.Options;

public class MyOptions { public int QueryDelay { get; set; } = 1000; }
public class Test {
    public static void Main() {
        IOptions<MyOptions> opt = Options.Create(new MyOptions());
        Console.WriteLine(opt.Value.QueryDelay);
    }
}
