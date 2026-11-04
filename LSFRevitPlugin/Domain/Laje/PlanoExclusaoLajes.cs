using System;
using System.Collections.Generic;
using System.Linq;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>Elemento do documento com a identidade de laje ja lida do Extensible Storage.</summary>
    public sealed class ElementoLajeIdentificado
    {
        public int ElementId { get; set; }

        public IdentidadeLaje Identidade { get; set; }
    }

    /// <summary>
    /// Plano puro de exclusao: quais elementos podem ser apagados e de quais lajes remover o
    /// registro. Nao conhece o Revit, entao pode ser testado sem abrir o documento.
    /// </summary>
    public sealed class PlanoExclusaoLaje
    {
        public bool Total { get; set; }

        public int LajeId { get; set; }

        public List<int> LajesIds { get; } = new List<int>();

        public List<int> ElementosIds { get; } = new List<int>();

        public List<string> Avisos { get; } = new List<string>();

        public bool TemAlvo => ElementosIds.Count > 0;
    }

    /// <summary>
    /// Monta o plano de exclusao a partir de elementos identificados. A fonte de verdade e
    /// sempre a identidade persistente (LajeId, Tipo, Papel, Numero); nada aqui depende de
    /// nome, comentario, categoria ou proximidade.
    /// </summary>
    public static class PlanoExclusaoLajes
    {
        public static PlanoExclusaoLaje Individual(
            int lajeId, IEnumerable<ElementoLajeIdentificado> elementos)
        {
            var plano = new PlanoExclusaoLaje { LajeId = lajeId };
            if (lajeId <= 0)
            {
                plano.Avisos.Add("Laje invalida para exclusao.");
                return plano;
            }

            List<ElementoLajeIdentificado> alvo = FiltrarValidos(elementos)
                .Where(e => e.Identidade.LajeId == lajeId)
                .ToList();

            Preencher(plano, alvo);
            plano.LajesIds.Add(lajeId);

            if (plano.ElementosIds.Count == 0)
                plano.Avisos.Add("Nenhum elemento da laje " + lajeId + " foi encontrado no documento.");

            return plano;
        }

        public static PlanoExclusaoLaje Total(IEnumerable<ElementoLajeIdentificado> elementos)
        {
            var plano = new PlanoExclusaoLaje { Total = true };
            List<ElementoLajeIdentificado> alvo = FiltrarValidos(elementos).ToList();

            Preencher(plano, alvo);
            plano.LajesIds.AddRange(
                alvo.Select(e => e.Identidade.LajeId).Distinct().OrderBy(id => id));

            if (plano.ElementosIds.Count == 0)
                plano.Avisos.Add("Nenhuma laje do LSF Modeler foi encontrada no documento.");

            return plano;
        }

        private static IEnumerable<ElementoLajeIdentificado> FiltrarValidos(
            IEnumerable<ElementoLajeIdentificado> elementos)
        {
            if (elementos == null) return Enumerable.Empty<ElementoLajeIdentificado>();
            return elementos.Where(e => e != null && e.Identidade != null && e.ElementId > 0);
        }

        private static void Preencher(
            PlanoExclusaoLaje plano, List<ElementoLajeIdentificado> alvo)
        {
            plano.ElementosIds.AddRange(
                alvo.Select(e => e.ElementId).Distinct().OrderBy(id => id));

            List<IGrouping<string, ElementoLajeIdentificado>> duplicados = alvo
                .GroupBy(e => Chave(e.Identidade))
                .Where(g => g.Select(e => e.ElementId).Distinct().Count() > 1)
                .ToList();

            foreach (IGrouping<string, ElementoLajeIdentificado> grupo in duplicados)
            {
                plano.Avisos.Add(
                    "Identidade duplicada em " + grupo.Count()
                    + " elementos (" + grupo.Key + "); todos serao removidos.");
            }
        }

        private static string Chave(IdentidadeLaje identidade)
        {
            return identidade.LajeId + "/" + identidade.Tipo + "/"
                + identidade.Papel + "/" + identidade.Numero;
        }
    }
}
