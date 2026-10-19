using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Models
{
    public enum FinalidadeBlocking
    {
        /// <summary>Travamento dos montantes (evitar flambagem do perfil na alma).</summary>
        Travamento,

        /// <summary>Apoio das placas de fechamento.</summary>
        Apoio,

        /// <summary>Travamento dos montantes e apoio das placas.</summary>
        Ambas
    }

    public static class FinalidadeBlockingExtensions
    {
        public static string Descricao(this FinalidadeBlocking finalidade)
        {
            switch (finalidade)
            {
                case FinalidadeBlocking.Travamento: return "Travamento dos montantes";
                case FinalidadeBlocking.Apoio: return "Apoio das placas";
                default: return "Travamento dos montantes e apoio das placas";
            }
        }
    }

    /// <summary>
    /// Distribuicao das fileiras de blocking ao longo do vao: em todos os vaos entre montantes
    /// adjacentes (padrao historico) ou apenas nos vaos alternados.
    /// </summary>
    public enum DistribuicaoBlocking
    {
        /// <summary>Gera blocking em todos os vaos entre montantes adjacentes (comportamento atual).</summary>
        Todos,

        /// <summary>
        /// Gera blocking apenas nos vaos alternados (a cada dois), de modo que os montantes
        /// sejam travados alternadamente e cada montante receba no maximo uma perna de blocking.
        /// </summary>
        Alternado
    }

    public sealed class BlockingConfig
    {
        public bool Habilitado { get; set; }

        /// <summary>
        /// Numero de fileiras automaticas (distribuicao uniforme entre as guias) usadas quando o
        /// usuario nao define alturas explicitas nem ha juntas de placas para alinhar.
        /// </summary>
        public int Fileiras { get; set; } = 1;

        /// <summary>Finalidade do blocking (travamento, apoio das placas ou ambas).</summary>
        public FinalidadeBlocking Finalidade { get; set; } = FinalidadeBlocking.Ambas;

        /// <summary>
        /// Como as fileiras se distribuem ao longo da parede: em todos os vaos entre montantes
        /// adjacentes (padrao) ou apenas nos vaos alternados.
        /// </summary>
        public DistribuicaoBlocking Distribuicao { get; set; } = DistribuicaoBlocking.Todos;

        /// <summary>
        /// Somente para <see cref="DistribuicaoBlocking.Alternado"/>: quando true, inverte a
        /// paridade do montante inicial, comecando os vaos pelo segundo montante em vez do primeiro.
        /// </summary>
        public bool AlternarInvertido { get; set; }

        /// <summary>
        /// Alturas das fileiras de blocking em centimetro, medidas da base da parede para cima,
        /// independentes das juntas das placas. Vazio = usar Fileiras automaticas ou o alinhamento
        /// as juntas das placas (se solicitado e disponivel).
        /// </summary>
        public List<double> AlturasRelativasCm { get; set; } = new List<double>();

        /// <summary>
        /// Quando true, alinha as fileiras as juntas horizontais das placas. So tem efeito se
        /// <see cref="AlturasJuntasPlacasCm"/> tiver dados; sem dados de placas o plugin NUNCA
        /// presume alturas e informa o usuario.
        /// </summary>
        public bool AlinharJuntasPlacas { get; set; }

        /// <summary>
        /// Alturas das juntas horizontais entre placas, em cm, medidas da base da parede. Preenchido
        /// pelo projeto quando ha placas geradas; vazio quando nao ha dados.
        /// </summary>
        public List<double> AlturasJuntasPlacasCm { get; set; } = new List<double>();

        public void Validar()
        {
            if (Fileiras < 0) Fileiras = 0;
            if (Fileiras > 5) Fileiras = 5;
            if (AlturasRelativasCm == null) AlturasRelativasCm = new List<double>();
            if (AlturasJuntasPlacasCm == null) AlturasJuntasPlacasCm = new List<double>();
            if (Distribuicao != DistribuicaoBlocking.Todos && Distribuicao != DistribuicaoBlocking.Alternado)
                Distribuicao = DistribuicaoBlocking.Todos;
        }
    }
}