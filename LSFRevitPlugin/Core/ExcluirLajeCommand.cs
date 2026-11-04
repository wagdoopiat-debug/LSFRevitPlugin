using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    /// <summary>Exclui uma unica laje do plugin, usando a identidade persistente dos elementos.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExcluirLajeCommand : IExternalCommand
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
                List<RegistroLaje> registros = RegistroLajesService.Listar(doc);
                if (registros.Count == 0)
                {
                    TaskDialog.Show(
                        "LSF Modeler - Excluir laje",
                        "Nenhuma laje registrada neste documento.");
                    return Result.Cancelled;
                }

                RegistroLaje escolhido = EscolherLaje(registros);
                if (escolhido == null) return Result.Cancelled;

                int encontrados = ExclusaoLajesService.Identificar(doc)
                    .Count(e => e.Identidade.LajeId == escolhido.LajeId);

                var confirmacao = new TaskDialog("LSF Modeler - Excluir laje")
                {
                    MainInstruction = "Excluir a laje " + escolhido.LajeId + "?",
                    MainContent =
                        "Serao removidos " + encontrados + " elemento(s) gerados por esta laje."
                        + Environment.NewLine
                        + "Outras lajes e elementos externos nao serao alterados.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };

                if (confirmacao.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                ResultadoExclusaoLaje resultado =
                    ExclusaoLajesService.Excluir(doc, escolhido.LajeId);

                Mostrar(resultado);
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

        private static RegistroLaje EscolherLaje(List<RegistroLaje> registros)
        {
            if (registros.Count == 1) return registros[0];

            var janela = new EscolhaLajeWindow(
                registros,
                "LSF Modeler - Excluir laje",
                "Escolha a laje para excluir:",
                "Excluir");
            return janela.ShowDialog() == true ? janela.Selecionado : null;
        }

        private static void Mostrar(ResultadoExclusaoLaje resultado)
        {
            string texto = resultado.Sucesso
                ? "Laje " + resultado.LajeId + " excluida. "
                    + resultado.ElementosRemovidos + " elemento(s) removido(s)."
                : "A laje nao foi excluida.";

            if (resultado.Avisos.Count > 0)
                texto += Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos);

            TaskDialog.Show("LSF Modeler - Excluir laje", texto);
        }
    }
}
