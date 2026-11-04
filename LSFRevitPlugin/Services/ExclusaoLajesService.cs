using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>Resultado de uma exclusao de laje(s), para mensagem ao usuario.</summary>
    public sealed class ResultadoExclusaoLaje
    {
        public bool Sucesso { get; set; }

        public bool Total { get; set; }

        public int LajeId { get; set; }

        public int ElementosEncontrados { get; set; }

        public int ElementosRemovidos { get; set; }

        public List<string> Avisos { get; } = new List<string>();
    }

    /// <summary>
    /// Exclui lajes geradas pelo plugin usando a identidade persistente por elemento. Varre o
    /// documento inteiro e so considera elementos cujo Extensible Storage aponta para uma laje
    /// do LSF. Elementos externos nunca entram na lista. Tudo acontece em uma unica transacao:
    /// se o Revit recusar qualquer exclusao, a transacao inteira sofre rollback.
    /// </summary>
    public static class ExclusaoLajesService
    {
        public static ResultadoExclusaoLaje Excluir(Document doc, int lajeId)
        {
            if (doc == null)
                return new ResultadoExclusaoLaje { Avisos = { "Documento invalido." } };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Individual(lajeId, Identificar(doc));
            return Executar(doc, plano);
        }

        public static ResultadoExclusaoLaje ExcluirTodas(Document doc)
        {
            if (doc == null)
                return new ResultadoExclusaoLaje { Avisos = { "Documento invalido." } };

            PlanoExclusaoLaje plano = PlanoExclusaoLajes.Total(Identificar(doc));
            return Executar(doc, plano);
        }

        /// <summary>
        /// Le a identidade de todos os elementos nao-tipo do documento. Nao filtra por categoria
        /// nem por nome: a identidade no Extensible Storage e o unico criterio.
        /// </summary>
        public static List<ElementoLajeIdentificado> Identificar(Document doc)
        {
            var encontrados = new List<ElementoLajeIdentificado>();
            if (doc == null) return encontrados;

            FilteredElementCollector coletor = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (Element elemento in coletor)
            {
                IdentidadeLaje identidade;
                if (!PertencenciaLajeService.TentarLer(elemento, out identidade)) continue;

                encontrados.Add(new ElementoLajeIdentificado
                {
                    ElementId = elemento.Id.IntegerValue,
                    Identidade = identidade
                });
            }

            return encontrados;
        }

        private static ResultadoExclusaoLaje Executar(Document doc, PlanoExclusaoLaje plano)
        {
            var resultado = new ResultadoExclusaoLaje
            {
                Total = plano.Total,
                LajeId = plano.LajeId,
                ElementosEncontrados = plano.ElementosIds.Count
            };
            resultado.Avisos.AddRange(plano.Avisos);

            if (!plano.TemAlvo) return resultado;

            using (var transacao = new Transaction(doc, "LSF Modeler - Excluir laje"))
            {
                transacao.Start();
                try
                {
                    var ids = plano.ElementosIds
                        .Select(id => new ElementId(id))
                        .Where(id => doc.GetElement(id) != null)
                        .ToList();

                    if (ids.Count > 0)
                    {
                        ICollection<ElementId> apagados = doc.Delete(ids);
                        resultado.ElementosRemovidos = apagados == null ? ids.Count : apagados.Count;
                    }

                    foreach (int id in plano.LajesIds)
                        RegistroLajesService.Remover(doc, id);

                    transacao.Commit();
                    resultado.Sucesso = true;
                }
                catch (Exception ex)
                {
                    if (transacao.HasStarted()) transacao.RollBack();
                    resultado.Avisos.Add("Exclusao cancelada: " + ex.Message);
                }
            }

            return resultado;
        }
    }
}
