namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tipo de trelica (vigota trelicada) da laje. Cada tipo descreve uma disposicao propria de
    /// banzos, diagonais e montantes (ver ComposicaoTrelica), sempre em funcao pura e sem Revit.
    /// Sao padroes geometricos de partida -- NAO representam validacao estrutural.
    /// </summary>
    public enum TrelicaTipo
    {
        /// <summary>Zigue-zague de diagonais alternadas, sem montantes verticais.</summary>
        Warren,

        /// <summary>Montantes verticais internos e diagonais inclinadas para o centro (tracao no centro).</summary>
        Pratt,

        /// <summary>Montantes verticais internos e diagonais inclinadas para fora (tracao nas extremidades).</summary>
        Howe,

        /// <summary>Montantes verticais internos e diagonais alternadas a cada painel.</summary>
        DiagonaisAlternadas,

        /// <summary>Somente banzos e montantes verticais internos (sem diagonais).</summary>
        MontantesVerticais
    }
}
