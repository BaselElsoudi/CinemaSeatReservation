namespace CinemaLogic

open System
open System.IO
open System.Text.Json

module Storage =

    open System.Text.Json

    let private serializerOptions =
        let opt = JsonSerializerOptions()
        opt.PropertyNameCaseInsensitive <- true
        opt.WriteIndented <- true
        opt

    /// Load state from JSON file. Returns empty state if file doesn't exist or is invalid.
    /// Handles migration from old format (State with SeatDto list) to new format (State with Reservation list).
    let loadState (path: string) : State =
        if File.Exists(path) then
            try
                let text = File.ReadAllText(path)
                if String.IsNullOrWhiteSpace(text) then
                    { reservations = [] }
                else
                    // Try new format first
                    try
                        let deserialized = JsonSerializer.Deserialize<State>(text, serializerOptions)
                        if isNull (box deserialized) then
                            { reservations = [] }
                        else
                            // Check if it's actually new format (has ticketId) or accidentally deserialized old format
                            let firstRes = deserialized.reservations |> List.tryHead
                            match firstRes with
                            | Some r when not (String.IsNullOrWhiteSpace(r.ticketId)) ->
                                // New format - valid
                                deserialized
                            | _ ->
                                // Old format was deserialized incorrectly - need to re-parse
                                // Try parsing as old format: { "reservations": [{"row":1,"col":1}, ...] }
                                use doc = JsonDocument.Parse(text)
                                let root = doc.RootElement
                                let mutable prop = Unchecked.defaultof<JsonElement>
                                if root.TryGetProperty("reservations", &prop) then
                                    let mutable migrated = []
                                    for item in prop.EnumerateArray() do
                                        let mutable rowProp = Unchecked.defaultof<JsonElement>
                                        let mutable colProp = Unchecked.defaultof<JsonElement>
                                        if item.TryGetProperty("row", &rowProp) && item.TryGetProperty("col", &colProp) then
                                            let row = rowProp.GetInt32()
                                            let col = colProp.GetInt32()
                                            let seat = { row = row; col = col }
                                            let res = {
                                                seat = seat
                                                ticketId = sprintf "%d-%d-migrated" row col
                                                client = None
                                            }
                                            migrated <- res :: migrated
                                    { reservations = List.rev migrated }
                                else
                                    { reservations = [] }
                    with
                    | _ -> { reservations = [] } // On error, return empty
            with
            | _ -> { reservations = [] } // On any error, return empty state
        else
            { reservations = [] } // File doesn't exist, return empty state

    /// Save state to JSON file. Creates directory if needed.
    let saveState (path: string) (state: State) : unit =
        let dir = Path.GetDirectoryName(path)
        if not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore
        
        let json = JsonSerializer.Serialize(state, serializerOptions)
        File.WriteAllText(path, json)
