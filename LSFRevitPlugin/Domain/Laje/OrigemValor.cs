namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// De onde vem um valor do catalogo. Serve para o usuario ver o que ainda precisa ser conferido.
    /// </summary>
    public enum OrigemValor
    {
        /// <summary>Confirmado em ficha do proprio produto OU decisao explicita do usuario.</summary>
        Conferido,

        /// <summary>Ficha de outro fabricante ou norma, usada na falta da ficha do produto.</summary>
        Referencia,

        /// <summary>Derivado de outros valores por formula.</summary>
        Calculado,

        /// <summary>Padrao proposto, a validar pelo usuario.</summary>
        Provisorio
    }
}
