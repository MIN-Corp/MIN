namespace MIN.Core.Identity.Contracts.Models;

/// <summary>
/// Вхождение identity для одного экземпляра приложения
/// </summary>
public sealed record IdentityEntry
{
    /// <summary>
    /// Слот, занятый приложением (по порядковому номеру мютекса)
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    /// Сохранённый идентификатор участника
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Имя участника
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Хеш идентификатора
    /// </summary>
    public string Hash { get; set; } = string.Empty;
}
