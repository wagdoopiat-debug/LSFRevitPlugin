using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Botao "Placas": cria as placas de fechamento das paredes que ja tem estrutura gerada, com
    /// juntas sobre os montantes, recortes dos vaos e reaproveitamento das sobras no projeto
    /// inteiro. O usuario escolhe tipo, medida e orientacao por painel e por face e marca os
    /// paineis que devem ser refeitos; os outros mantem as placas que ja tem.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PlacasCommand : IExternalCommand
    {
        private const string PrefixoEstrutura = "LSF | Parede ";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            try
            {
                List<Wall> paredes = ParedesComEstrutura(doc);
                if (paredes.Count == 0)
                {
                    TaskDialog.Show(
                        "LSF Modeler - Placas",
                        "Nenhuma parede com estrutura gerada foi encontrada. Gere a estrutura das paredes antes.");
                    return Result.Cancelled;
                }

                var leituras = new List<LeituraParede>();
                var avisosLeitura = new List<string>();
                foreach (Wall parede in paredes)
                {
                    try
                    {
                        leituras.Add(LeituraEstruturaParedeService.Ler(doc, parede));
                    }
                    catch (InvalidOperationException ex)
                    {
                        avisosLeitura.Add("Parede " + parede.Id + ": " + ex.Message);
                    }
                }

                if (!leituras.Any(l => l.TemEstrutura))
                {
                    TaskDialog.Show("LSF Modeler - Placas", "Nenhuma parede com estrutura pode ser lida.");
                    return Result.Cancelled;
                }

                PlacasExistentes existentes = PlacasExistentes.Ler(doc);

                var janela = new EscolhaPlacasWindow(leituras, existentes);
                new WindowInteropHelper(janela).Owner = commandData.Application.MainWindowHandle;
                janela.ShowDialog();

                if (!janela.Confirmado) return Result.Cancelled;

                ResumoPlacas resumo = PlacasService.Gerar(doc, leituras, existentes, janela.Seletor, janela.Refazer);
                resumo.Avisos.InsertRange(0, avisosLeitura);

                TaskDialog.Show("LSF Modeler - Placas", MontarRelatorio(resumo));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("LSF Modeler - Erro", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>Paredes que tem elementos de estrutura gerados (pelo texto do Comentario).</summary>
        private static List<Wall> ParedesComEstrutura(Document doc)
        {
            var ids = new HashSet<int>();

            foreach (DirectShape ds in new FilteredElementCollector(doc)
                .OfClass(typeof(DirectShape))
                .Cast<DirectShape>())
            {
                string comentario = ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
                if (comentario == null || !comentario.StartsWith(PrefixoEstrutura, StringComparison.Ordinal)) continue;

                string resto = comentario.Substring(PrefixoEstrutura.Length);
                int fim = resto.IndexOf(' ');
                int id;
                if (fim > 0 && int.TryParse(resto.Substring(0, fim), out id)) ids.Add(id);
            }

            return ids
                .OrderBy(i => i)
                .Select(i => doc.GetElement(new ElementId(i)) as Wall)
                .Where(w => w != null)
                .ToList();
        }

        private static string MontarRelatorio(ResumoPlacas resumo)
        {
            var texto = new StringBuilder();
            texto.AppendLine(
                resumo.Paredes + " parede(s), " + resumo.Paineis + " painel(is): "
                + resumo.PaineisRefeitos + " refeito(s), " + resumo.PaineisMantidos + " mantido(s).");
            texto.AppendLine(
                resumo.Pecas + " pecas de placa criadas: " + resumo.PecasComRecorte + " com recorte, "
                + resumo.PecasReaproveitadas + " saidas de sobras.");

            if (resumo.PecasComFalha > 0)
            {
                texto.AppendLine("ATENCAO: " + resumo.PecasComFalha + " peca(s) nao puderam ser criadas.");
            }

            if (resumo.LinhasPorEstoque.Count > 0)
            {
                texto.AppendLine();
                foreach (string linha in resumo.LinhasPorEstoque) texto.AppendLine(linha);
            }

            if (resumo.Avisos.Count > 0)
            {
                texto.AppendLine();
                texto.AppendLine("Avisos (" + resumo.Avisos.Count + "):");
                foreach (string aviso in resumo.Avisos.Take(10)) texto.AppendLine("- " + aviso);
                if (resumo.Avisos.Count > 10) texto.AppendLine("... e mais " + (resumo.Avisos.Count - 10) + ".");
            }

            return texto.ToString().TrimEnd();
        }
    }
}