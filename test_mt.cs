using System;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class Test 
{
    public void Configure() 
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x => {
            x.AddEntityFrameworkOutbox<DbContext>(o => {
                o.QueryDelay = TimeSpan.FromSeconds(60);
                o.QueryTimeout = TimeSpan.FromSeconds(30);
            });
        });
    }
}
