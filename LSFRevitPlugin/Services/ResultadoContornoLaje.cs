using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Resultado da extraccao de um contorno de laje a partir de uma origem (piso, bordas ou
    /// pontos). Segue o padrao do projeto: problemas entram como Erros (impedem) ou Avisos
    /// (nao impedem). Guarda a origem para a interface e para a persistencia.
    /// </summary>
    public sealed class ResultadoContornoLaje
    {
        public ContornoLaje Contorno { get; set; }

        /// <summary>Cota Z (mm) sugerida como topo do conjunto; o usuario pode ajustar.</summary>
        public double CotaMm { get; set; }

        public TipoOrigemContorno Origem { get; set; }

        /// <summary>Elemento de origem (ex.: o piso), quando houver; nulo para bordas/pontos.</summary>
        public ElementId ElementoOrigem { get; set; }

        /// <summary>Quantos lacos internos (furos) foram encontrados e ignorados (v1: so o externo).</summary>
        public int QuantidadeFuros { get; set; }

        public List<string> Avisos { get; } = new List<string>();

        public List<string> Erros { get; } = new List<string>();

        public bool EhValido => Erros.Count == 0 && Contorno != null;
    }
}
