using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Extrai um contorno a partir de linhas de modelo/detalhe selecionadas no Revit. Cada
    /// elemento vira um SegmentoContorno (somente linhas retas na v1) e o ContornoLaje monta o
    /// poligono fechado, unindo extremos proximos. Elementos que nao sao linhas retas entram como
    /// aviso e sao ignorados.
    /// </summary>
    public static class ContornoDeLinhasService
    {
        /// <summary>
        /// Converte os elementos informados em segmentos de contorno. Encadeia e valida via
        /// ContornoLaje.MontarDeSegmentos.
        /// </summary>
        public static ResultadoContornoLaje Extrair(IEnumerable<Element> elementos)
        {
            var resultado = new ResultadoContornoLaje { Origem = TipoOrigemContorno.BordasSelecionadas };

            if (elementos == null)
            {
                resultado.Erros.Add("Nenhuma borda informada.");
                return resultado;
            }

            var segmentos = new List<SegmentoContorno>();
            double somaZ = 0.0;
            int contagemZ = 0;

            foreach (Element elemento in elementos)
            {
                Line linha = ObterLinha(elemento);
                if (linha == null)
                {
                    resultado.Avisos.Add(
                        "Elemento ignorado (nao e uma linha reta): Id "
                        + (elemento?.Id.IntegerValue.ToString() ?? "?") + ".");
                    continue;
                }

                XYZ a = linha.GetEndPoint(0);
                XYZ b = linha.GetEndPoint(1);

                segmentos.Add(new SegmentoContorno(ParaMm(a), ParaMm(b)));

                somaZ += a.Z + b.Z;
                contagemZ += 2;
            }

            if (segmentos.Count < 3)
            {
                resultado.Erros.Add("Sao necessarias pelo menos 3 linhas retas para formar o contorno.");
                return resultado;
            }

            List<string> erros;
            ContornoLaje contorno = ContornoLaje.MontarDeSegmentos(segmentos, out erros);

            if (contorno == null)
            {
                resultado.Erros.AddRange(erros);
                return resultado;
            }

            resultado.Avisos.AddRange(erros);

            List<string> errosContorno = contorno.Validar();
            if (errosContorno.Count > 0)
            {
                resultado.Erros.AddRange(errosContorno);
                return resultado;
            }

            resultado.Contorno = contorno;
            resultado.CotaMm = contagemZ > 0
                ? Core.RevitVersionAdapter.InternoParaMilimetros(somaZ / contagemZ)
                : 0.0;
            return resultado;
        }

        private static Line ObterLinha(Element elemento)
        {
            if (elemento is CurveElement curva && curva.GeometryCurve is Line linhaCurva)
            {
                return linhaCurva;
            }

            if (elemento?.Location is LocationCurve loc && loc.Curve is Line linhaLoc)
            {
                return linhaLoc;
            }

            return null;
        }

        private static Ponto2DMm ParaMm(XYZ ponto)
        {
            return new Ponto2DMm(
                Core.RevitVersionAdapter.InternoParaMilimetros(ponto.X),
                Core.RevitVersionAdapter.InternoParaMilimetros(ponto.Y));
        }
    }
}
