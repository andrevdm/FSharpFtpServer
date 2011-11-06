namespace FtpServer
    open System
    open System.IO
    open System.Net
    open System.Net.Sockets
    open System.Threading
    open System.Text
    open System.Text.RegularExpressions

    open Settings
    open SocketExtensions

    type ISocketHandler =
        abstract member Process : Socket -> Async<unit>

    [<AbstractClass>]
    type LineProtocolHandler() =
        let mutable stop = false

        member t.Send code line (socket:Socket) =
            async{
                if debug then printfn "< %d %s" code line

                let! sent = socket.AsyncSend( Encoding.ASCII.GetBytes( sprintf "%d %s\r\n" code line ) )
                ignore()
            }

        abstract member OnConnect : Socket -> Async<unit>
        abstract member Handle : String * Socket -> Async<unit>

        member t.Stop() =
            stop <- true        

        member t.SplitCmd( line ) =
            let m = Regex.Match( line, @"^(?<cmd>[^ ]+)( (?<rest>.*))?" )
            if not m.Success then failwith ("invalid command: " + line)
            (m.Groups.["cmd"].Value, m.Groups.["rest"].Value)

        member t.WithCmd( line, expectedCmd, f:string->string->unit, ?e:string->string->unit ) =
            let cmd, rest = t.SplitCmd line 
        
            if cmd <> expectedCmd then 
                let e = defaultArg e (fun a e -> failwith (sprintf "Invalid command, got '%s' expecting '%s'" a e))
                e cmd expectedCmd

            f cmd rest

        interface ISocketHandler with
            member t.Process( socket ) =
                async{
                    do! t.OnConnect( socket )
                
                    let lineBuffer = new StringBuilder()
                    let read = ref 1
                
                    while !read >= 1 && not stop do
                        let buffer = Array.create 1 0uy
                        let! read = socket.AsyncReceive(buffer)
                        let c = char buffer.[0]

                        match c with
                            | '\n' -> ignore()
                            | '\r' -> 
                                let line = lineBuffer.ToString()
                                lineBuffer.Clear() |> ignore
                                if debug then printfn "> %s" line

                                do! t.Handle( line, socket )

                            | _ -> lineBuffer.Append( c ) |> ignore
                }