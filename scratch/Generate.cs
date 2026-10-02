using System;
using System.IO;
using System.Text.RegularExpressions;

class Program {
    static void Main() {
        var files = new[] {
            "src/alphazero-api/Modules/Documents/Infrastructure/Consumers/DocumentQueriesConsumer.cs",
            "src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Saga/RevokeStudentEnrollmentFromSagaConsumer.cs",
            "src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Saga/EnrollStudentFromSagaConsumer.cs",
            "src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Analytics/CourseAnalyticsUpdaterConsumer.cs",
            "src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Videos/VideoEventHandlers.cs",
            "src/alphazero-api/Modules/Courses/Infrastructure/Consumers/Assessments/AssessmentGradingCompletedConsumer.cs",
            "src/alphazero-api/Modules/VideoUploading/Infrastructure/Consumers/Queries/GetVideoMetadataConsumer.cs",
            "src/alphazero-api/Modules/VideoUploading/Infrastructure/Consumers/VideoStateLifecycleConsumers.cs",
            "src/alphazero-api/Modules/VideoUploading/Infrastructure/Consumers/VideoS3SyncHandlers.cs",
            "src/alphazero-api/Modules/Assessments/Infrastructure/Consumers/CreateAssessmentRequestHandler.cs",
            "src/alphazero-api/Modules/Identity/Infrastructure/Consumers/Saga/RemoveStudentRoleFromSagaConsumer.cs",
            "src/alphazero-api/Modules/Identity/Infrastructure/Consumers/Saga/AssignStudentRoleFromSagaConsumer.cs"
        };
        
        foreach (var file in files) {
            var text = File.ReadAllText(file);
            if (text.Contains("abstract class")) continue;
            
            var nsMatch = Regex.Match(text, @"namespace ([\w\.]+);?");
            if (!nsMatch.Success) continue;
            var ns = nsMatch.Groups[1].Value;
            var module = ns.Split('.')[2]; // AlphaZero.Modules.MODULE_NAME
            
            var matches = Regex.Matches(text, @"class (\w+)[^\{]*IConsumer<");
            foreach (Match match in matches) {
                var className = match.Groups[1].Value;
                var outFile = file.Replace(Path.GetFileName(file), className + "Definition.cs");
                var content = $@"using MassTransit;
using AlphaZero.Modules.{module}.Infrastructure.Persistance;

namespace {ns};

public class {className}Definition : ConsumerDefinition<{className}>
{{
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<{className}> consumerConfigurator, IRegistrationContext context)
    {{
        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);
    }}
}}
";
                File.WriteAllText(outFile, content);
                Console.WriteLine($"Generated {outFile}");
            }
        }
    }
}
