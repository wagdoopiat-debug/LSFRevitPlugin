namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tipo de laje: seca (piso seco sobre as vigas) ou umida (telha metalica TP40 + argamassa).
    /// </summary>
    public enum TipoLaje
    {
        Seca,
        Umida
    }

    /// <summary>
    /// Tipo de viga da laje. Na v1 so PerfilSimples esta implementado; os demais entram como
    /// variacoes em tarefas proprias.
    /// </summary>
    public enum TipoVigaLaje
    {
        PerfilSimples,
        Caixao,
        BackToBack,
        Trelica
    }
}
