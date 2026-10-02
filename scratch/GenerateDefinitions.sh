#!/bin/bash
for f in $(find src -type f -name "*.cs" -exec grep -l "IConsumer<" {} +); do
    # Skip SQS consumers
    if [[ "$f" == *"SQS"* ]]; then
        continue
    fi
    # Skip base classes if they are abstract
    if grep -q "abstract class" "$f"; then
        continue
    fi

    # Extract namespace and class names
    NAMESPACE=$(grep "^namespace " "$f" | sed -e 's/namespace //' -e 's/;//' | tr -d '\r' | xargs)
    if [ -z "$NAMESPACE" ]; then
        NAMESPACE=$(grep "^namespace" "$f" -A 1 | tail -n 1 | sed -e 's/{//' | tr -d '\r' | xargs)
    fi
    
    # We may have multiple consumers in one file
    # grep "class " $f
    CONSUMERS=$(grep "class " "$f" | grep -v "abstract " | grep -v "partial " | awk '{print $3}' | awk -F':' '{print $1}' | awk -F'<' '{print $1}' | tr -d '\r' | xargs)
    
    # Find the module's DbContext namespace
    # e.g. AlphaZero.Modules.Documents.Infrastructure.Persistance.AppDbContext
    MODULE=$(echo "$NAMESPACE" | awk -F'.' '{print $3}')
    DBCONTEXT="AlphaZero.Modules.${MODULE}.Infrastructure.Persistance.AppDbContext"
    
    if [ -z "$CONSUMERS" ]; then continue; fi

    echo "// Auto-generated ConsumerDefinitions for $f" > "${f%.cs}Definition.cs"
    echo "using MassTransit;" >> "${f%.cs}Definition.cs"
    echo "using ${DBCONTEXT%AppDbContext};" >> "${f%.cs}Definition.cs"
    echo "namespace $NAMESPACE;" >> "${f%.cs}Definition.cs"
    echo "" >> "${f%.cs}Definition.cs"
    
    for c in $CONSUMERS; do
        # Ignore non-consumer classes in the same file
        if ! grep -q "class $c .*IConsumer" "$f"; then
            continue
        fi
        
        echo "public class ${c}Definition : ConsumerDefinition<${c}>" >> "${f%.cs}Definition.cs"
        echo "{" >> "${f%.cs}Definition.cs"
        echo "    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<${c}> consumerConfigurator, IRegistrationContext context)" >> "${f%.cs}Definition.cs"
        echo "    {" >> "${f%.cs}Definition.cs"
        echo "        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);" >> "${f%.cs}Definition.cs"
        echo "    }" >> "${f%.cs}Definition.cs"
        echo "}" >> "${f%.cs}Definition.cs"
        echo "" >> "${f%.cs}Definition.cs"
    done
done
