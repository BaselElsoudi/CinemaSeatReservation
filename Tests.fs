namespace CinemaLogic.Tests

open System
open Xunit
open CinemaLogic
open CinemaLogic.Logic

module Tests =

    // Helper function to create test state
    let createState (reservations: Reservation list) : State =
        { reservations = reservations }

    // Helper function to create a reservation
    let createReservation row col ticketId clientOpt =
        {
            seat = { row = row; col = col }
            ticketId = ticketId
            client = clientOpt
        }

    [<Fact>]
    let ``reserveSeats: Success when seats are free`` () =
        let initialState = createState []
        let seats = [| { row = 1; col = 1 } |]
        let newState, result = Logic.reserveSeats initialState seats None
        
        Assert.True(result.success)
        Assert.Equal(1, newState.reservations.Length)
        Assert.Equal(1, result.ticketIds.Length)
        Assert.Equal(0, result.failed.Length)
        Assert.Contains("Successfully reserved", result.message)

    [<Fact>]
    let ``reserveSeats: Fails when seat is already reserved`` () =
        let existing = createReservation 1 1 "ticket-1" None
        let initialState = createState [existing]
        let seats = [| { row = 1; col = 1 } |]
        let newState, result = Logic.reserveSeats initialState seats None
        
        Assert.False(result.success)
        Assert.Equal(1, newState.reservations.Length) // State unchanged
        Assert.Equal(0, result.ticketIds.Length)
        Assert.Equal(1, result.failed.Length)
        Assert.Contains("already reserved", result.message)

    [<Fact>]
    let ``reserveSeats: Success with multiple seats`` () =
        let initialState = createState []
        let seats = [| { row = 1; col = 1 }; { row = 1; col = 2 }; { row = 2; col = 1 } |]
        let newState, result = Logic.reserveSeats initialState seats (Some "John Doe")
        
        Assert.True(result.success)
        Assert.Equal(3, newState.reservations.Length)
        Assert.Equal(3, result.ticketIds.Length)
        Assert.Equal(0, result.failed.Length)
        Assert.NotNull(newState.reservations.Head.client)
        Assert.Equal(Some "John Doe", newState.reservations.Head.client)

    [<Fact>]
    let ``reserveSeats: Fails when some seats are already reserved`` () =
        let existing = createReservation 1 1 "ticket-1" None
        let initialState = createState [existing]
        let seats = [| { row = 1; col = 1 }; { row = 1; col = 2 } |]
        let newState, result = Logic.reserveSeats initialState seats None
        
        Assert.False(result.success)
        Assert.Equal(1, newState.reservations.Length) // Only existing one remains
        Assert.Equal(0, result.ticketIds.Length)
        Assert.Equal(1, result.failed.Length)
        Assert.Equal({ row = 1; col = 1 }, result.failed.[0])

    [<Fact>]
    let ``reserveSeats: Ticket ID format is correct`` () =
        let initialState = createState []
        let seats = [| { row = 5; col = 10 } |]
        let newState, result = Logic.reserveSeats initialState seats None
        
        Assert.True(result.success)
        let ticketId = result.ticketIds.[0]
        Assert.StartsWith("5-10-", ticketId)
        // Format: row-col-yyyyMMddHHmmss (14 digits for timestamp)
        let parts = ticketId.Split('-')
        Assert.Equal(3, parts.Length)
        Assert.Equal("5", parts.[0])
        Assert.Equal("10", parts.[1])
        Assert.Equal(14, parts.[2].Length) // yyyyMMddHHmmss = 14 characters

    [<Fact>]
    let ``deleteReservations: Successfully deletes single reservation`` () =
        let existing = createReservation 1 1 "ticket-1" (Some "John")
        let initialState = createState [existing]
        let seats = [| { row = 1; col = 1 } |]
        let newState, result = Logic.deleteReservations initialState seats
        
        Assert.True(result.success)
        Assert.Equal(0, newState.reservations.Length)
        Assert.Contains("Deleted", result.message)

    [<Fact>]
    let ``deleteReservations: Successfully deletes multiple reservations`` () =
        let r1 = createReservation 1 1 "ticket-1" None
        let r2 = createReservation 1 2 "ticket-2" None
        let r3 = createReservation 2 1 "ticket-3" None
        let initialState = createState [r1; r2; r3]
        let seats = [| { row = 1; col = 1 }; { row = 1; col = 2 } |]
        let newState, result = Logic.deleteReservations initialState seats
        
        Assert.True(result.success)
        Assert.Equal(1, newState.reservations.Length) // Only r3 remains
        Assert.Equal(r3, newState.reservations.Head)
        Assert.Contains("Deleted 2 reservation", result.message)

    [<Fact>]
    let ``deleteReservations: No error when deleting non-existent seat`` () =
        let initialState = createState []
        let seats = [| { row = 1; col = 1 } |]
        let newState, result = Logic.deleteReservations initialState seats
        
        Assert.True(result.success)
        Assert.Equal(0, newState.reservations.Length)
        Assert.Contains("Deleted 0 reservation", result.message)

    [<Fact>]
    let ``deleteReservations: Only deletes specified seats`` () =
        let r1 = createReservation 1 1 "ticket-1" None
        let r2 = createReservation 1 2 "ticket-2" None
        let r3 = createReservation 2 1 "ticket-3" None
        let initialState = createState [r1; r2; r3]
        let seats = [| { row = 1; col = 1 } |]
        let newState, result = Logic.deleteReservations initialState seats
        
        Assert.True(result.success)
        Assert.Equal(2, newState.reservations.Length)
        Assert.Contains(r2, newState.reservations)
        Assert.Contains(r3, newState.reservations)
        Assert.DoesNotContain(r1, newState.reservations)

    [<Fact>]
    let ``listReservations: Returns empty array for empty state`` () =
        let initialState = createState []
        let result = Logic.listReservations initialState
        
        Assert.NotNull(result)
        Assert.Equal(0, result.Length)

    [<Fact>]
    let ``listReservations: Returns all reservations with correct data`` () =
        let r1 = createReservation 1 1 "ticket-1" (Some "Alice")
        let r2 = createReservation 1 2 "ticket-2" None
        let r3 = createReservation 2 1 "ticket-3" (Some "Bob")
        let initialState = createState [r1; r2; r3]
        let result = Logic.listReservations initialState
        
        Assert.Equal(3, result.Length)
        
        // Check first reservation
        Assert.Equal({ row = 1; col = 1 }, result.[0].seat)
        Assert.Equal("ticket-1", result.[0].ticketId)
        Assert.Equal(Some "Alice", result.[0].client)
        
        // Check second reservation
        Assert.Equal({ row = 1; col = 2 }, result.[1].seat)
        Assert.Equal("ticket-2", result.[1].ticketId)
        Assert.Equal(None, result.[1].client)
        
        // Check third reservation
        Assert.Equal({ row = 2; col = 1 }, result.[2].seat)
        Assert.Equal("ticket-3", result.[2].ticketId)
        Assert.Equal(Some "Bob", result.[2].client)

    [<Fact>]
    let ``listReservations: Preserves all reservation details`` () =
        let r1 = createReservation 5 10 "custom-ticket-id" (Some "Test Client")
        let initialState = createState [r1]
        let result = Logic.listReservations initialState
        
        Assert.Equal(1, result.Length)
        Assert.Equal(r1.seat, result.[0].seat)
        Assert.Equal(r1.ticketId, result.[0].ticketId)
        Assert.Equal(r1.client, result.[0].client)

    [<Fact>]
    let ``buildLayout: Creates layout with correct dimensions and reserved seats`` () =
        let r1 = createReservation 1 1 "ticket-1" None
        let r2 = createReservation 2 3 "ticket-2" None
        let initialState = createState [r1; r2]
        let layout = Logic.buildLayout initialState 6 10
        
        Assert.Equal(6, layout.rows)
        Assert.Equal(10, layout.cols)
        Assert.Equal(2, layout.reserved.Length)
        Assert.Contains({ row = 1; col = 1 }, layout.reserved)
        Assert.Contains({ row = 2; col = 3 }, layout.reserved)

    [<Fact>]
    let ``buildLayout: Returns empty reserved array when no reservations`` () =
        let initialState = createState []
        let layout = Logic.buildLayout initialState 5 8
        
        Assert.Equal(5, layout.rows)
        Assert.Equal(8, layout.cols)
        Assert.Equal(0, layout.reserved.Length)

    [<Fact>]
    let ``Integration: Reserve then delete then list`` () =
        // Start with empty state
        let state1 = createState []
        
        // Reserve a seat
        let seats1 = [| { row = 3; col = 4 } |]
        let state2, result1 = Logic.reserveSeats state1 seats1 (Some "Integration Test")
        Assert.True(result1.success)
        Assert.Equal(1, state2.reservations.Length)
        
        // List reservations
        let list1 = Logic.listReservations state2
        Assert.Equal(1, list1.Length)
        Assert.Equal(Some "Integration Test", list1.[0].client)
        
        // Delete the reservation
        let seats2 = [| { row = 3; col = 4 } |]
        let state3, result2 = Logic.deleteReservations state2 seats2
        Assert.True(result2.success)
        Assert.Equal(0, state3.reservations.Length)
        
        // List again - should be empty
        let list2 = Logic.listReservations state3
        Assert.Equal(0, list2.Length)

    [<Fact>]
    let ``Integration: Multiple operations sequence`` () =
        let mutable state = createState []
        
        // Reserve 3 seats
        let seats1 = [| { row = 1; col = 1 }; { row = 1; col = 2 }; { row = 2; col = 1 } |]
        let state1, _ = Logic.reserveSeats state seats1 (Some "Client A")
        state <- state1
        Assert.Equal(3, state.reservations.Length)
        
        // Try to reserve already reserved seat - should fail
        let seats2 = [| { row = 1; col = 1 } |]
        let state2, result2 = Logic.reserveSeats state seats2 (Some "Client B")
        Assert.False(result2.success)
        Assert.Equal(3, state2.reservations.Length) // Unchanged
        
        // Delete one reservation
        let seats3 = [| { row = 1; col = 2 } |]
        let state3, _ = Logic.deleteReservations state seats3
        state <- state3
        Assert.Equal(2, state.reservations.Length)
        
        // Now reserve that seat again - should succeed
        let seats4 = [| { row = 1; col = 2 } |]
        let state4, result4 = Logic.reserveSeats state seats4 (Some "Client C")
        Assert.True(result4.success)
        Assert.Equal(3, state4.reservations.Length)
        
        // List all - should have 3 reservations
        let finalList = Logic.listReservations state4
        Assert.Equal(3, finalList.Length)
