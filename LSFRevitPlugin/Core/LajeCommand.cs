using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// COMANDO FINAL da laje (T14): criar uma laje nova.
    /// Oferece apenas duas origens (piso existente do Revit ou desenhar o contorno por cliques),
    /// extrai o contorno, abre a janela de configuracao (com previa) e gera a laje num LajeId novo.
    /// O registro por documento (RegistroLajesService) guarda a origem e os parametros, o que
    /// permite listar e editar depois.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LajeCommand : IExternalCommand
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
                ResultadoContornoLaje origem = ObterContorno(uiDoc);
                if (origem == null) return Result.Cancelled;

                if (!origem.EhValido)
                {
                    TaskDialog.Show(
                        "LSF Modeler - Contorno invalido",
                        string.Join("\n", origem.Erros));
                    return Result.Cancelled;
                }

                var janela = new LajeConfigWindow(
                    origem.Contorno,
                    origem.Origem,
                    origem.CotaMm,
                    CatalogoLaje.CriarPadrao(),
                    existente: null,
                    posicoesApoioProvider: o => ApoiosLajeService.PosicoesApoioMm(doc, origem.Contorno, o, out _),
                    toleranciaAlinhamentoMm: CriteriosLaje.CriarPadrao().ToleranciaAlinhamentoMm.Valor);

                if (janela.ShowDialog() != true) return Result.Cancelled;

                int lajeId = RegistroLajesService.ProximoLajeId(doc);
                int origemId = origem.ElementoOrigem != null
                    ? origem.ElementoOrigem.IntegerValue
                    : -1;

                ResultadoGeracaoLaje resultado = new LajeGenerationService().Gerar(
                    doc,
                    janela.Resultado,
                    lajeId,
                    origem.Origem,
                    origemId);

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

        private static ResultadoContornoLaje ObterContorno(UIDocument uiDoc)
        {
            var pergunta = new TaskDialog("LSF Modeler - Nova laje");
            pergunta.MainInstruction = "Como definir o contorno da laje?";
            pergunta.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                "Usar piso existente do Revit",
                "Usa o contorno do piso selecionado (o piso nao e alterado).");
            pergunta.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Desenhar contorno da laje",
                "Voce clica os vertices do contorno no plano da vista. "
                + "O Revit 2021 nao permite abrir o modo de esboco nativo a partir de um comando "
                + "externo, entao os cliques equivalem ao esboco.");
            pergunta.CommonButtons = TaskDialogCommonButtons.Cancel;

            switch (pergunta.Show())
            {
                case TaskDialogResult.CommandLink1:
                    return ExtrairDePiso(uiDoc);
                case TaskDialogResult.CommandLink2:
                    return ExtrairDePontos(uiDoc);
                default:
                    return null;
            }
        }

        private static ResultadoContornoLaje ExtrairDePiso(UIDocument uiDoc)
        {
            Reference referencia = uiDoc.Selection.PickObject(
                ObjectType.Element,
                new FiltroPiso(),
                "Selecione o piso que define o contorno da laje.");

            Floor piso = uiDoc.Document.GetElement(referencia) as Floor;
            return ContornoDePisoService.Extrair(uiDoc.Document, piso);
        }

        private static ResultadoContornoLaje ExtrairDeLinhas(UIDocument uiDoc)
        {
            IList<Reference> referencias = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new FiltroLinha(),
                "Selecione as linhas do contorno (Enter para terminar).");

            var elementos = new List<Element>();
            foreach (Reference r in referencias)
            {
                Element e = uiDoc.Document.GetElement(r);
                if (e != null) elementos.Add(e);
            }

            return ContornoDeLinhasService.Extrair(elementos);
        }

        private static ResultadoContornoLaje ExtrairDePontos(UIDocument uiDoc)
        {
            var pontos = new List<XYZ>();

            while (true)
            {
                try
                {
                    XYZ ponto = uiDoc.Selection.PickPoint(
                        "Clique os vertices do contorno (Esc para terminar).");
                    pontos.Add(ponto);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
            }

            return ContornoDePontosService.Extrair(pontos);
        }

        private sealed class FiltroPiso : ISelectionFilter
        {
            public bool AllowElement(Element elemento)
            {
                return elemento is Floor;
            }

            public bool AllowReference(Reference referencia, XYZ posicao)
            {
                return false;
            }
        }

        private sealed class FiltroLinha : ISelectionFilter
        {
            public bool AllowElement(Element elemento)
            {
                return elemento is CurveElement;
            }

            public bool AllowReference(Reference referencia, XYZ posicao)
            {
                return false;
            }
        }
    }
}
