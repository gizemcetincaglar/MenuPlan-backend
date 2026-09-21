namespace Menulux.Api.DTOs;

public enum TableStatus
{
    Empty = 0,
    Occupied = 1,
    Reserved = 2,
}

public record TableDto(
    Guid Id,
    string Name,
    string? Zone,
    int Capacity,
    bool IsReserved,
    TableStatus Status,
    decimal ActiveBillTotal,
    decimal ActiveBillRemaining);

public record CreateTableRequest(string Name, string? Zone, int Capacity);

public record UpdateTableRequest(string Name, string? Zone, int Capacity);

public record SetTableReservedRequest(bool Reserved);
