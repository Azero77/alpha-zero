using System;
using MassTransit;
using MassTransit.Transports;
using MassTransit.DependencyInjection;

public class Test<TBus> where TBus: class, IBus {
    public IBusInstance<TBus> inst;
}
