using System.Collections.Generic;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Resultado da geracao (ou regeracao) de uma laje. Segue o padrao de ResultadoGeracao: os
    /// problemas entram como Erros (impedem o uso) ou Avisos (nao impedem) no resultado, e nao
    /// como excecoes soltas. Traz a medicao de uma viga para o teste manual.
    /// </summary>
    public sealed class ResultadoGeracaoLaje
    {
        public int LajeId { get; set; }

        public int VigasCriadas { get; set; }

        public int GuiasBordaCriadas { get; set; }

        /// <summary>Trelicas de travamento de borda criadas (travamento das trelicas nos dois apoios).</summary>
        public int TravamentosCriados { get; set; }

        /// <summary>Fitas tensionadas de travamento entre trelicas criadas.</summary>
        public int FitasCriadas { get; set; }

        /// <summary>Quantas placas de cobertura (OSB/cimenticia/piso seco) foram criadas.</summary>
        public int CamadasCriadas { get; set; }

        /// <summary>Quantos fixadores (parafusos) foram criados.</summary>
        public int ParafusosCriados { get; set; }

        /// <summary>Quantas chapas de telha foram criadas.</summary>
        public int TelhasCriadas { get; set; }

        /// <summary>Quantas placas de argamassa/enchimento foram criados.</summary>
        public int ArgamassasCriadas { get; set; }

        /// <summary>Quantas chapas gusset foram criadas nas juntas da trelica.</summary>
        public int ChapasGussetCriadas { get; set; }

        /// <summary>Quantos elementos da mesma laje foram apagados antes de regerar (0 na primeira vez).</summary>
        public int RemovidosAnteriores { get; set; }

        public int TotalCriados =>
            VigasCriadas + GuiasBordaCriadas + TravamentosCriados + FitasCriadas + CamadasCriadas
            + ParafusosCriados + TelhasCriadas + ArgamassasCriadas + ChapasGussetCriadas;

        /// <summary>Comprimento medido de uma viga gerada (LeitorDeEstrutura.Medir), em mm; nulo se nao mediu.</summary>
        public double? ComprimentoVigaMedidoMm { get; set; }

        /// <summary>Altura da alma (extensao Z) de uma viga gerada, em mm; nulo se nao mediu.</summary>
        public double? AlturaAlmaMedidaMm { get; set; }

        public List<string> Avisos { get; } = new List<string>();

        public List<string> Erros { get; } = new List<string>();

        public bool EhValido => Erros.Count == 0;
    }
}
