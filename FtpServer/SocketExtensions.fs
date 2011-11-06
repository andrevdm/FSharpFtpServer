namespace FtpServer
    open System
    open System.Net
    open System.Net.Sockets

    module SocketExtensions =
        type Socket with
          member socket.AsyncAccept() = Async.FromBeginEnd(socket.BeginAccept, socket.EndAccept)

          member socket.AsyncReceive(buffer:byte[], ?offset, ?count) =
            let offset = defaultArg offset 0
            let count = defaultArg count buffer.Length
            let beginReceive(b,o,c,cb,s) = socket.BeginReceive(b,o,c,SocketFlags.None,cb,s)
            Async.FromBeginEnd(buffer, offset, count, beginReceive, socket.EndReceive)
  
          member socket.AsyncSend(buffer:byte[], ?offset, ?count) =
            let offset = defaultArg offset 0
            let count = defaultArg count buffer.Length
            let beginSend(b,o,c,cb,s) = socket.BeginSend(b,o,c,SocketFlags.None,cb,s)
            Async.FromBeginEnd(buffer, offset, count, beginSend, socket.EndSend)
