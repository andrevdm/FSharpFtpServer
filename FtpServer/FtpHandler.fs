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

    type ftpLoginState = 
        | ExpectUserName
        | ExpectPassword
        | LoggedIn

    type FtpHandler( dirProvider:IDirectoryProvider ) =
        inherit LineProtocolHandler()
        let mutable loginState = ExpectUserName
        let mutable userName = ""
        let mutable ipAddr = IPAddress.None
        let mutable port = -1
        let mutable active = true
        let mutable binaryMode = true

        override t.OnConnect( socket ) =
            async{
                do! t.Send 220 "Hello" socket
            }

        member t.Error( code, err, socket ) =
            async{
                do! t.Send code err socket
            }

        member t.CanLogin( userName:string, password:string ) =
            true

        member t.SetPort( p, socket ) =
            async{
                let m = Regex.Match( p, @"(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)" )
                if not m.Success then 
                    do! t.Error( 501, "invald port parameters", socket )
                else
                    ipAddr <- IPAddress.Parse( sprintf "%s.%s.%s.%s" m.Groups.[1].Value m.Groups.[2].Value m.Groups.[3].Value m.Groups.[4].Value )
                    port <- (Convert.ToInt32( m.Groups.[5].Value ) * 256) + Convert.ToInt32( m.Groups.[6].Value )
                    do! t.Send 200 "PORT command successful" socket
            }

        member t.SendList( p, socket ) =
            async{
                do! t.Send 150 "Opening ASCII mode data connection for /bin/ls" socket
            
                use sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                let endpoint = IPEndPoint(ipAddr, port)
                sender.Connect( endpoint )
            
                let! sent = sender.AsyncSend( Encoding.ASCII.GetBytes( dirProvider.List() ) )
                do! t.Send 226 "Listing completed." socket
            }

        member t.RetrieveFile( r, socket ) =
            async{
                do! t.Send 150 ("Opening BINARY mode data connection for " + r) socket
            
                use sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                let endpoint = IPEndPoint(ipAddr, port)
                sender.Connect( endpoint )

                let! ok = dirProvider.DownloadFile( r, sender )
            
                if ok then
                    do! t.Send 226 "Download completed." socket
                else
                    do! t.Send 426 "Download failed." socket
            }        

        member t.HandleCommand( line, socket ) =
            async{
                let c,r = t.SplitCmd line

                match c with
                    | "PORT" -> do! t.SetPort( r, socket )
                    | "NLST" 
                    | "LIST" -> do! t.SendList( r, socket )                    
                    | "PWD" -> do! t.Send 257 ("\"" + dirProvider.CurrentPath + "\" is the current directory") socket
                    | "TYPE" -> do! t.Send 200 "ignored" socket
                    | "RETR" -> do! t.RetrieveFile( r, socket )
                    | "CWD" ->
                        if dirProvider.ChangeDir( r ) then do! t.Send 200 "directory changed" socket
                        else do! t.Send 552 "Invalid directory" socket
                    | _ -> do! t.Send 502 "Command not implemented" socket

            }

        member t.HandleLoginUserName( line, socket ) =
            async{
                let c,r = t.SplitCmd line

                if c <> "USER" then
                    do! t.Send 530 "please login" socket
                else 
                    do! t.Send 331 "user name okay, need password." socket
                    loginState <- ExpectPassword                 
            }

        member t.HandleLoginPassword( line, socket ) =
            async{
                let c,r = t.SplitCmd line

                if c <> "PASS" then
                    do! t.Send 530 "please login" socket
                    loginState <- ExpectUserName
                else 
                    if t.CanLogin( userName, r ) then
                        do! t.Send 230 "User logged in, proceed" socket
                        loginState <- LoggedIn                 
                    else
                        do! t.Send 530 "invalid password, please login" socket
                        loginState <- ExpectUserName
            }

        override t.Handle( line, socket ) =
            async{
                if line = "QUIT" then 
                    t.Stop()
                else        
                    match loginState with
                        | ExpectUserName -> do! t.HandleLoginUserName( line, socket )
                        | ExpectPassword -> do! t.HandleLoginPassword( line, socket )
                        | LoggedIn -> do! t.HandleCommand( line, socket )
                        | _ -> failwith ("unknown ftpLoginState " + loginState.ToString())
            }

