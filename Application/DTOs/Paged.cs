namespace Application.DTOs;

public record Paged<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);
