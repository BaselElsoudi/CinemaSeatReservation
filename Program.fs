namespace CinemaLogic

open System
open System.IO
open System.Text.Json
open Logic
open Storage

module Program =

    let serializerOptions =
        let o = JsonSerializerOptions()
        o.PropertyNameCaseInsensitive <- true
        o.PropertyNamingPolicy <- null
        o.WriteIndented <- false
        o

    let tryParse (json:string) =
        try
            if String.IsNullOrWhiteSpace(json) then None
            else
                let dto = JsonSerializer.Deserialize<ReqDto>(json, serializerOptions)
                let dtoOpt = Option.ofObj (box dto) |> Option.map (fun x -> x :?> ReqDto)
                match dtoOpt with
                | None -> None
                | Some d ->
                    if String.IsNullOrWhiteSpace(d.action) then None
                    else
                        let a = d.action.Trim().ToLowerInvariant()
                        match a with
                        | "get_layout" ->
                            let r = defaultArg d.rows 0
                            Some (GetLayout(r, d.cols))
                        | "reserve" ->
                            let s = defaultArg d.seats [||]
                            Some (Reserve(s, d.client))
                        | "delete_reservations" ->
                            let s = defaultArg d.seats [||]
                            Some (DeleteReservations(s))
                        | "list_reservations" ->
                            Some ListReservations
                        | "savestate" ->
                            Some SaveState
                        | _ -> None
        with _ ->
            None

    let writeResp (resp:RespDto) =
        let json = JsonSerializer.Serialize(resp, serializerOptions)
        Console.Out.WriteLine(json)
        Console.Out.Flush()

    [<EntryPoint>]
    let main argv =

        // Debug file to confirm program starts
        try
            let dbgPath = Path.Combine(Path.GetTempPath(), "cinema_debug_started.txt")
            File.WriteAllText(dbgPath, DateTime.UtcNow.ToString("o"))
        with _ -> ()

        let cwd = Directory.GetCurrentDirectory()
        let storagePath = Path.Combine(cwd, "data", "seats_storage.json")
        let defaultRows = 6
        let defaultCols = 10

        // Read stdin or fallback to argv
        let raw =
            try
                let t = Console.In.ReadToEnd()
                if String.IsNullOrWhiteSpace(t) then
                    if argv.Length > 0 then argv.[0] else ""
                else t
            with _ ->
                if argv.Length > 0 then argv.[0] else ""

        let inp = raw.Trim()

        if inp = "" then
            writeResp { status="error"; message=Some "Empty request"; layout=None; ticketIds=None; failed=None; reservations=None }
            1
        else
            match tryParse inp with
            | None ->
                writeResp { status="error"; message=Some "Invalid request"; layout=None; ticketIds=None; failed=None; reservations=None }
                1

            | Some req ->
                try
                    let abs = storagePath
                    let dir = Path.GetDirectoryName(abs)
                    if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore

                    let state = Storage.loadState abs

                    match req with
                    | GetLayout (rows, colsOpt) ->
                        let r = if rows > 0 then rows else defaultRows
                        let c = defaultArg colsOpt defaultCols
                        let layout = Logic.buildLayout state r c
                        writeResp { status="ok"; message=None; layout=Some layout; ticketIds=None; failed=None; reservations=None }
                        0

                    | Reserve (seats, client) ->
                        let bad =
                            seats |> Array.filter (fun s -> s.row <= 0 || s.col <= 0)

                        if bad.Length > 0 then
                            writeResp {
                                status="error"; message=Some "Invalid seat coords";
                                layout=None; ticketIds=None; failed=Some bad; reservations=None
                            }
                            1
                        else
                            let newState, result = Logic.reserveSeats state seats client
                            Storage.saveState abs newState
                            writeResp {
                                status = (if result.success then "ok" else "fail")
                                message = Some result.message
                                layout=None; ticketIds=Some result.ticketIds; failed=Some result.failed; reservations=None
                            }
                            0

                    | DeleteReservations seats ->
                        let bad = seats |> Array.filter (fun s -> s.row <= 0 || s.col <= 0)
                        if bad.Length > 0 then
                            writeResp {
                                status="error"; message=Some "Invalid seat coords";
                                layout=None; ticketIds=None; failed=Some bad; reservations=None
                            }
                            1
                        else
                            let newState, result = Logic.deleteReservations state seats
                            Storage.saveState abs newState
                            writeResp {
                                status = (if result.success then "ok" else "fail")
                                message = Some result.message
                                layout=None; ticketIds=None; failed=None; reservations=None
                            }
                            0

                    | ListReservations ->
                        let reservations = Logic.listReservations state
                        writeResp {
                            status="ok"
                            message=Some (sprintf "Found %d reservation(s)" reservations.Length)
                            layout=None; ticketIds=None; failed=None; reservations=Some reservations
                        }
                        0

                    | SaveState ->
                        Storage.saveState abs state
                        writeResp { status="ok"; message=Some "saved"; layout=None; ticketIds=None; failed=None; reservations=None }
                        0

                    | Unknown ->
                        writeResp { status="error"; message=Some "Unknown request"; layout=None; ticketIds=None; failed=None; reservations=None }
                        1

                with ex ->
                    writeResp { status="error"; message=Some ex.Message; layout=None; ticketIds=None; failed=None; reservations=None }
                    2
