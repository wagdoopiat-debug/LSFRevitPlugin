using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Constroi um contorno a partir de pontos desenhados pelo usuario (PickPoint no plano da
    /// vista). Os pontos sao projetados no plano da planta (X, Y) e a cota sugerida e a media das
    /// cotas Z capturadas.
    /// </summary>
    public static class ContornoDePontosService
    {
        public static ResultadoContornoLaje Extrair(IEnumerable<XYZ> pontos)
        {
            var resultado = new ResultadoContornoLaje { Origem = TipoOrigemContorno.PontosDesenhados };

            if (pontos == null)
            {
                resultado.Erros.Add("Nenhum ponto informado.");
                return resultado;
            }

            var lista = new List<Ponto2DMm>();
            double somaZ = 0.0;
            int contagem = 0;

            foreach (XYZ ponto in pontos)
            {
                lista.Add(ParaMm(ponto));
                somaZ += ponto.Z;
                contagem++;
            }

            if (lista.Count < 3)
            {
                resultado.Erros.Add("Sao necessarios pelo menos 3 pontos para formar o contorno.");
                return resultado;
            }

            var contorno = new ContornoLaje(lista);
            List<string> errosContorno = contorno.Validar();
            if (errosContorno.Count > 0)
            {
                resultado.Erros.AddRange(errosContorno);
                return resultado;
            }

            resultado.Contorno = contorno;
            resultado.CotaMm = contagem > 0
                ? Core.RevitVersionAdapter.InternoParaMilimetros(somaZ / contagem)
                : 0.0;
            return resultado;
        }

        private static Ponto2DMm ParaMm(XYZ ponto)
        {
            return new Ponto2DMm(
                Core.RevitVersionAdapter.InternoParaMilimetros(ponto.X),
                Core.RevitVersionAdapter.InternoParaMilimetros(ponto.Y));
        }
    }
}
