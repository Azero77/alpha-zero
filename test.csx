using System.Reflection;
using System.Linq;
var asm = Assembly.LoadFrom("src/alphazero-api/Modules/VideoUploading/Application/bin/Debug/net9.0/AlphaZero.Modules.VideoUploading.Application.dll");
var type = asm.GetType("AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession.CreatePlaybackSessionCommand");
var interfaces = type.GetInterfaces();
foreach (var i in interfaces) {
    Console.WriteLine(i.FullName);
}
