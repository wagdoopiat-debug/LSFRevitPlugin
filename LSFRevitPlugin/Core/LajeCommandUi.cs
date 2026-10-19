using Autodesk.Revit.UI;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Dialogo comum aos comandos de laje para mostrar o resultado de uma geracao/regeneracao,
    /// evitando repetir a mesma mensagem em Criar e Editar.
    /// </summary>
    internal static class LajeCommandUi
    {
        public static void MostrarResultado(ResultadoGeracaoLaje r)
        {
            if (r == null) return;

            if (!r.EhValido)
            {
                TaskDialog.Show(
                    "LSF Modeler - Laje nao gerada",
                    string.Join("\n", r.Erros));
                return;
            }

            string medicao = r.ComprimentoVigaMedidoMm.HasValue
                ? "\n\nViga medida: " + r.ComprimentoVigaMedidoMm.Value.ToString("F0")
                    + " mm de comprimento, alma de "
                    + r.AlturaAlmaMedidaMm.GetValueOrDefault().ToString("F0") + " mm."
                : string.Empty;

            string avisos = r.Avisos.Count > 0 ? "\n\nAvisos:\n" + string.Join("\n", r.Avisos) : string.Empty;

            TaskDialog.Show(
                "LSF Modeler - Laje gerada",
                "Laje " + r.LajeId + ": " + r.VigasCriadas + " viga(s) + "
                + r.GuiasBordaCriadas + " guia(s) de borda"
                + (r.CamadasCriadas > 0 ? " + " + r.CamadasCriadas + " camada(s) de placa." : ".")
                + (r.RemovidosAnteriores > 0
                    ? "\nElementos anteriores removidos: " + r.RemovidosAnteriores + "."
                    : string.Empty)
                + medicao + avisos);
        }
    }
}
