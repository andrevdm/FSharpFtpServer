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

    type IDirectoryProvider =
        abstract member CurrentPath : string with get
        abstract member List : unit -> string
        abstract member ChangeDir : string -> bool
        abstract member DownloadFile : string * Socket -> Async<bool>


    type FileSystemDirectoryProvider( root:string ) =
        let mutable cwd = "\\"
        let mutable root = root
        let mutable physicalDirectory = ""

        do
            let d = new DirectoryInfo( root )
            root <- d.FullName
            physicalDirectory <- d.FullName   

        interface IDirectoryProvider with 
            member t.CurrentPath
                with get() =
                    cwd

            member t.ChangeDir( dir ) =
                //TODO needs fixing
                let d = Path.Combine( root, if dir.StartsWith( "\\" ) then dir.Substring( 1 ) else dir )
                if (Directory.Exists( d )) then 
                    physicalDirectory <- d
                    cwd <- physicalDirectory.Replace( root, "" )
                    true
                else
                    false

            member t.List() =
                let dir = new StringBuilder()
                
                let info = new DirectoryInfo( physicalDirectory )

                info.GetFiles()
                    |> Array.iter (fun f -> 
                        dir.AppendFormat( "-r--r--r--  1   owner   group {1} 1970 01 01  {0}", f.Name, f.Length ).AppendLine() |> ignore )
            
                info.GetDirectories()
                    |> Array.iter (fun d -> 
                        dir.AppendFormat( "dr--r--r--  1   owner   group {1} 1970 01 01  {0}", d.Name, 0 ).AppendLine() |> ignore )

                dir.ToString()

            member t.DownloadFile( fileName:string, sender:Socket ) =
                async{
                    let path = Path.Combine( physicalDirectory, fileName )
                    use strm = File.OpenRead( path )

                    let toSend = ref strm.Length
                    let buffer = Array.create 2048 0uy

                    while !toSend > 0L do
                        let read = strm.Read( buffer, 0, 2048 )
                        let! s = sender.AsyncSend( buffer, 0, read )
                        toSend := !toSend - int64 s

                    return true
                }

