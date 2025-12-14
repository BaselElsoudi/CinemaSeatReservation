namespace CinemaLogic

open System

module Logic =

    /// Build a layout DTO from state and dimensions
    let buildLayout (state: State) (rows: int) (cols: int) : LayoutDto =
        let reservedArray = state.reservations |> List.map (fun r -> r.seat) |> List.toArray
        { rows = rows; cols = cols; reserved = reservedArray }

    /// Reserve seats. Returns new state and result.
    let reserveSeats (state: State) (seats: SeatDto[]) (clientOpt: string option) : State * ReservationResult =
        // Check which seats are already reserved
        let reservedSeats = state.reservations |> List.map (fun r -> r.seat) |> Set.ofList
        let alreadyReserved = 
            seats 
            |> Array.filter (fun s -> reservedSeats.Contains(s))
            |> Array.toList

        if alreadyReserved.Length > 0 then
            // Some seats already reserved - fail
            let result = {
                success = false
                message = sprintf "Some seats are already reserved"
                ticketIds = [||]
                failed = alreadyReserved |> List.toArray
            }
            state, result
        else
            // All seats available - reserve them
            // Ticket ID format: "row-col-yyyyMMddHHmmss"
            // Example: "1-2-20251212180254" = seat 1-2 reserved on 2025-12-12 at 18:02:54 UTC
            // Format breakdown: row-col-YYYY(4)MM(2)DD(2)HH(24)MM(2)SS(2)
            let timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            let newReservations = 
                seats 
                |> Array.map (fun s -> {
                    seat = s
                    ticketId = sprintf "%d-%d-%s" s.row s.col timestamp
                    client = clientOpt
                })
                |> Array.toList
            
            let newState = { reservations = state.reservations @ newReservations }
            
            // Generate ticket IDs
            let ticketIds = newReservations |> List.map (fun r -> r.ticketId) |> List.toArray
            
            let result = {
                success = true
                message = sprintf "Successfully reserved %d seat(s)" seats.Length
                ticketIds = ticketIds
                failed = [||]
            }
            newState, result

    /// Delete reservations for specified seats. Returns new state and result.
    let deleteReservations (state: State) (seats: SeatDto[]) : State * ReservationResult =
        let seatsToDelete = seats |> Set.ofArray
        let remaining, deleted = 
            state.reservations 
            |> List.partition (fun r -> not (seatsToDelete.Contains(r.seat)))
        
        let deletedSeats = deleted |> List.map (fun r -> r.seat) |> List.toArray
        let newState = { reservations = remaining }
        
        let result = {
            success = true
            message = sprintf "Deleted %d reservation(s)" deleted.Length
            ticketIds = [||]
            failed = [||]
        }
        newState, result

    /// List all reservations with their details
    let listReservations (state: State) : ReservationInfoDto[] =
        state.reservations 
        |> List.map (fun r -> 
            { 
                ReservationInfoDto.seat = r.seat
                ReservationInfoDto.ticketId = r.ticketId
                ReservationInfoDto.client = r.client
            })
        |> List.toArray
