using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// COMANDO FINAL da laje (T14): editar/regenerar uma laje existente.
    /// Le o registro por documento (RegistroLajesService), deixa escolher a laje, reabre a janela
    /// de configuracao ja preenchida com o que foi escolhido antes e regenera mantendo o MESMO
    /// LajeId. Se a origem era um piso e ele ainda existe, o contorno e relido (reflete edicoes
    /// no piso); caso contrario usa o contorno guardado no registro.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class EditarLajeCommand : IExternalCommand
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
                        "LSF Modeler - Editar laje",
                        "Nenhuma laje registrada neste documento. Use \"Criar laje\" primeiro.");
                    return Result.Cancelled;
                }

                RegistroLaje escolhido = EscolherLaje(registros);
                if (escolhido == null) return Result.Cancelled;

                ParametrosLaje existente = escolhido.Parametros;
                if (existente == null)
                {
                    TaskDialog.Show("LSF Modeler - Editar laje", "O registro da laje esta incompleto.");
                    return Result.Cancelled;
                }

                ReextrairContornoSePiso(doc, escolhido, existente);

                var janela = new LajeConfigWindow(
                    existente.Contorno,
                    escolhido.OrigemTipo,
                    existente.CotaMm,
                    CatalogoLaje.CriarPadrao(),
                    existente,
                    posicoesApoioProvider: o => ApoiosLajeService.PosicoesApoioMm(doc, existente.Contorno, o, out _),
                    toleranciaAlinhamentoMm: CriteriosLaje.CriarPadrao().ToleranciaAlinhamentoMm.Valor);

                if (janela.ShowDialog() != true) return Result.Cancelled;

                ResultadoGeracaoLaje resultado = new LajeGenerationService().Gerar(
                    doc,
                    janela.Resultado,
                    escolhido.LajeId,
                    escolhido.OrigemTipo,
                    escolhido.OrigemElementoId);

                LajeCommandUi.MostrarResultado(resultado);
                return resultado.EhValido ? Result.Succeeded : Result.Failed;
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

            var janela = new EscolhaLajeWindow(registros);
            return janela.ShowDialog() == true ? janela.Selecionado : null;
        }

        /// <summary>
        /// Se a origem era um piso e ele ainda existe, relê o contorno do piso (reflete edicoes
        /// feitas no piso desde a primeira geracao). Se o piso foi apagado ou a releitura falhar,
        /// mantem o contorno guardado no registro.
        /// </summary>
        private static void ReextrairContornoSePiso(
            Document doc,
            RegistroLaje registro,
            ParametrosLaje parametros)
        {
            if (registro.OrigemTipo != TipoOrigemContorno.PisoExistente) return;
            if (registro.OrigemElementoId < 0) return;

            var id = new ElementId(registro.OrigemElementoId);
            Floor piso = doc.GetElement(id) as Floor;
            if (piso == null) return;

            ResultadoContornoLaje atual = ContornoDePisoService.Extrair(doc, piso);
            if (!atual.EhValido || atual.Contorno == null) return;

            parametros.Contorno = atual.Contorno;
            parametros.CotaMm = atual.CotaMm;
        }
    }
}
