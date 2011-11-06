open System
open FtpServer


let createHandler() = 
    new FtpHandler( new FileSystemDirectoryProvider(@"c:\temp\")  ) :> ISocketHandler

let ftpDaemon = new SocketServer(createHandler, 21)
let task = Async.StartAsTask( ftpDaemon.Start() )

Console.ReadLine() |> ignore
Console.WriteLine( "shutting down" )
task.Dispose()