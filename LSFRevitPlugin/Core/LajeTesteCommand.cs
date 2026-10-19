using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// COMANDO TEMPORARIO da T11 (nao e o comando final da laje, que sera a T14).
    /// Existe apenas para permitir o teste manual/visual da primeira geracao da laje no Revit:
    /// gera um retangulo de 6000 x 4000 mm no ponto clicado, com perfil padrao e espacamento de
    /// 400 mm, e mostra a medicao de uma viga (LeitorDeEstrutura.Medir). Sera substituido.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LajeTesteCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                XYZ ponto = uiDoc.Selection.PickPoint(
                    "Clique o canto inferior-esquerdo da laje de teste (6000 x 4000 mm).");

                double x0Mm = LeitorDeEstrutura.Mm(ponto.X);
                double y0Mm = LeitorDeEstrutura.Mm(ponto.Y);

                double cotaMm = 0.0;
                Level nivel = doc.ActiveView.GenLevel;
                if (nivel != null) cotaMm = LeitorDeEstrutura.Mm(nivel.Elevation);

                var contorno = new ContornoLaje(new[]
                {
                    new Ponto2DMm(x0Mm, y0Mm),
                    new Ponto2DMm(x0Mm + 6000.0, y0Mm),
                    new Ponto2DMm(x0Mm + 6000.0, y0Mm + 4000.0),
                    new Ponto2DMm(x0Mm, y0Mm + 4000.0),
                });

                var parametros = new ParametrosLaje
                {
                    Contorno = contorno,
                    CotaMm = cotaMm,
                    EspacamentoMm = 400.0,
                    Tipo = TipoLaje.Umida,
                    Perfil = PerfilSpec.CriarPadrao(),
                };

                ResultadoGeracaoLaje r = new LajeGenerationService().Gerar(doc, parametros, 1);

                string medicao = r.ComprimentoVigaMedidoMm.HasValue
                    ? string.Format(
                        "\n\nMedicao (LeitorDeEstrutura.Medir): comprimento da viga = {0:F1} mm; altura da alma = {1:F1} mm.",
                        r.ComprimentoVigaMedidoMm.Value, r.AlturaAlmaMedidaMm.GetValueOrDefault())
                    : "";

                string avisos = r.Avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", r.Avisos) : "";
                string erros = r.Erros.Count > 0 ? "\n\nErros:\n" + string.Join("\n", r.Erros) : "";

                TaskDialog.Show(
                    "LSF Modeler - Laje (teste T11)",
                    string.Format(
                        "Laje {0} gerada: {1} viga(s) + {2} guia(s) de borda; {3} elemento(s) anterior(es) removido(s).{4}{5}{6}",
                        r.LajeId, r.VigasCriadas, r.GuiasBordaCriadas, r.RemovidosAnteriores, medicao, avisos, erros));

                return r.EhValido ? Result.Succeeded : Result.Failed;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("LSF Modeler - Erro", ex.Message);
                return Result.Failed;
            }
        }
    }
}
