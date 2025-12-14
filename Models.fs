namespace CinemaLogic

open System

// JSON DTOs for input/output
type SeatDto = { row: int; col: int }

type LayoutDto = { rows: int; cols: int; reserved: SeatDto[] }

type ReqDto = {
    action: string
    rows: int option
    cols: int option
    seats: SeatDto[] option
    client: string option
}

type ReservationResult = {
    success: bool
    message: string
    ticketIds: string[]
    failed: SeatDto[]
}

type ReservationInfoDto = {
    seat: SeatDto
    ticketId: string
    client: string option
}

type RespDto = {
    status: string
    message: string option
    layout: LayoutDto option
    ticketIds: string[] option
    failed: SeatDto[] option
    reservations: ReservationInfoDto[] option
}

// Internal domain types - reservation with full details
type Reservation = {
    seat: SeatDto
    ticketId: string
    client: string option
}

type State = { reservations: Reservation list }

// Request discriminated union for internal processing
type Request =
    | GetLayout of rows: int * cols: int option
    | Reserve of seats: SeatDto[] * client: string option
    | DeleteReservations of seats: SeatDto[]
    | ListReservations
    | SaveState
    | Unknown
