namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Centraliza as conversoes de unidade do plugin. O resto do plugin nunca deve
    /// converter unidades direto -- sempre atraves desta classe.
    /// As conversoes sao feitas por aritmetica pura (1 pe = 30,48 cm = 304,8 mm,
    /// definicao exata), sem chamar a API de unidades do Revit. Assim funcionam
    /// iguais em qualquer versao do Revit e tambem fora dele (testes unitarios).
    /// </summary>
    public static class RevitVersionAdapter
    {
        private const double CentimetrosPorPe = 30.48;
        private const double MilimetrosPorPe = 304.8;

        /// <summary>
        /// Converte um valor em centimetros (unidade de trabalho do plugin) para
        /// unidades internas do Revit (pes).
        /// </summary>
        public static double CentimetrosParaInterno(double valorCm)
        {
            return valorCm / CentimetrosPorPe;
        }

        /// <summary>
        /// Converte um valor em unidades internas do Revit (pes) de volta para
        /// centimetros, para exibicao ou calculo em unidades metricas.
        /// </summary>
        public static double InternoParaCentimetros(double valorInterno)
        {
            return valorInterno * CentimetrosPorPe;
        }

        /// <summary>
        /// Converte milimetros (unidade do motor de calculo estrutural, Domain/) para
        /// unidades internas do Revit.
        /// </summary>
        public static double MilimetrosParaInterno(double valorMm)
        {
            return valorMm / MilimetrosPorPe;
        }
    }
}