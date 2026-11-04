using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Exclui todas as lajes do plugin no documento. A varredura usa a identidade persistente,
    /// entao elementos externos (paredes, paineis, familias etc.) nunca entram na exclusao.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExcluirTodasLajesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                List<ElementoLajeIdentificado> identificados =
                    ExclusaoLajesService.Identificar(doc);
                int lajes = identificados
                    .Select(e => e.Identidade.LajeId)
                    .Distinct()
                    .Count();

                if (lajes == 0)
                {
                    TaskDialog.Show(
                        "LSF Modeler - Excluir todas as lajes",
                        "Nenhuma laje do LSF Modeler foi encontrada neste documento.");
                    return Result.Cancelled;
                }

                var confirmacao = new TaskDialog("LSF Modeler - Excluir todas as lajes")
                {
                    MainInstruction = "Excluir todas as " + lajes + " lajes do LSF Modeler?",
                    MainContent =
                        "Serao removidos " + identificados.Count + " elemento(s) gerados pelo plugin."
                        + Environment.NewLine
                        + "Elementos externos nao serao alterados. A operacao nao pode ser desfeita"
                        + " pelo plugin.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };

                if (confirmacao.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                ResultadoExclusaoLaje resultado = ExclusaoLajesService.ExcluirTodas(doc);
                Mostrar(resultado, lajes);
                return resultado.Sucesso ? Result.Succeeded : Result.Failed;
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

        private static void Mostrar(ResultadoExclusaoLaje resultado, int lajes)
        {
            string texto = resultado.Sucesso
                ? lajes + " laje(s) excluida(s). "
                    + resultado.ElementosRemovidos + " elemento(s) removido(s)."
                : "As lajes nao foram excluidas.";

            if (resultado.Avisos.Count > 0)
                texto += Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos);

            TaskDialog.Show("LSF Modeler - Excluir todas as lajes", texto);
        }
    }
}
