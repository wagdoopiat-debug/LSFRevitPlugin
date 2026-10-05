namespace LSFRevitPlugin.Domain.Models
{
    public class ResultadoGeracao
    {
        public int MontantesPrincipais { get; set; }
        public int EncontrosL { get; set; }
        public int MontantesH { get; set; }
        public int HJaExistentes { get; set; }
        public int EncontrosT { get; set; }
        public int MontantesTriplo { get; set; }
        public int TriploJaExistentes { get; set; }
        public int EncontrosCruz { get; set; }
        public int MontantesQuadruplo { get; set; }
        public int QuadruploJaExistentes { get; set; }
        public int Reposicionados { get; set; }
        public int IsoladosPuladosPorT { get; set; }
        public int TotalCriados { get; set; }
        public double MaiorVaoCm { get; set; }
        public bool VaoExcedeuLimite { get; set; }

        public int VaosDetectados { get; set; }
        public int VaosJaReforcados { get; set; }
        public int ElementosDeVao { get; set; }

        public int GuiasCriadas { get; set; }
    }
}