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
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
                // Can we do o.InboxCleanupDelay ?
            });
        });
    }
}
