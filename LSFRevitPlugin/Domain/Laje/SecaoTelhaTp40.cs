using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Perfil transversal puro da telha trapezoidal TP40, para a geometria 3D. Devolve o contorno
    /// fechado (poligono) da chapa dobrada em coordenadas locais: X = posicao ao longo da largura
    /// da telha (mm, comecando em zero) e Y = altura acima da base da telha (mm).
    ///
    /// O perfil e um "anel fino": a linha de fundo da onda seguida de volta pela linha de topo
    /// deslocada da espessura da chapa. Nao e validacao estrutural nem ficha de fabricante:
    /// usa os valores do catalogo TelhaTp40 (parte deles provisoria).
    /// </summary>
    public static class SecaoTelhaTp40
    {
        /// <summary>Espessura minima modelada (mm), pela mesma razao da chapa de Drywall no Revit.</summary>
        public const double EspessuraMinimaModeladaMm = 1.0;

        /// <summary>
        /// Contorno fechado da chapa, com <paramref name="onduasAltas"/> ondas altas (normalmente
        /// OndasAltasPorTelha). A largura total modelada e o numero de ondas x passo.
        /// </summary>
        public static List<Ponto2DMm> Contorno(TelhaTp40 telha, double espessuraMm)
        {
            if (telha == null) throw new ArgumentNullException(nameof(telha));

            double t = EspessuraModelada(telha, espessuraMm);
            List<Ponto2DMm> fundo = PerfilInferior(telha);

            var contorno = new List<Ponto2DMm>(fundo.Count * 2);
            contorno.AddRange(fundo);

            for (int i = fundo.Count - 1; i >= 0; i--)
            {
                contorno.Add(new Ponto2DMm(fundo[i].XMm, fundo[i].YMm + t));
            }

            return contorno;
        }

        /// <summary>
        /// Linha de fundo da onda (a face inferior da chapa), em coordenadas locais: X = posicao
        /// ao longo da largura da telha (mm, comecando em zero) e Y = altura acima da base da
        /// telha (mm). E uma linha poligonal (nao fecha): sobe nas rampas e fica plana no fundo e
        /// na crista. Usada para varrer/recortar a telha pelo contorno da laje.
        /// </summary>
        public static List<Ponto2DMm> PerfilInferior(TelhaTp40 telha)
        {
            if (telha == null) throw new ArgumentNullException(nameof(telha));

            List<string> erros = telha.Validar();
            if (erros.Count > 0)
                throw new ArgumentException("Telha invalida: " + string.Join(" ", erros), nameof(telha));

            double passo = telha.LarguraUtilMm.Valor / telha.OndasAltasPorTelha.Valor;
            double altura = telha.AlturaOndaMm.Valor;
            double baseOnda = telha.BaseOndaAltaMm.Valor;
            double crista = telha.CristaOndaMm.Valor;
            int ondas = (int)Math.Round(telha.OndasAltasPorTelha.Valor);

            double w1 = (passo - baseOnda) / 2.0;   // trecho baixo plano
            double w2 = (baseOnda - crista) / 2.0;  // rampa
            double w3 = crista;                     // crista plana

            var fundo = new List<Ponto2DMm>();
            for (int k = 0; k < ondas; k++)
            {
                double u0 = k * passo;
                Adicionar(fundo, u0, 0.0);
                Adicionar(fundo, u0 + w1, 0.0);
                Adicionar(fundo, u0 + w1 + w2, altura);
                Adicionar(fundo, u0 + w1 + w2 + w3, altura);
                Adicionar(fundo, u0 + w1 + w2 + w3 + w2, 0.0);
                Adicionar(fundo, u0 + passo, 0.0);
            }

            return fundo;
        }

        /// <summary>Espessura efetiva modelada (mm): a informada, com o minimo da chapa.</summary>
        public static double EspessuraModelada(TelhaTp40 telha, double espessuraMm)
        {
            return Math.Max(espessuraMm, EspessuraMinimaModeladaMm);
        }

        /// <summary>Largura total modelada da chapa (mm): ondas x passo.</summary>
        public static double LarguraModeladaMm(TelhaTp40 telha)
        {
            return telha.LarguraUtilMm.Valor;
        }

        /// <summary>
        /// Recobrimento lateral efetivo (passo de onda). Por padrao 2 ondas = 2 * PassoOndaMm.
        /// </summary>
        public static double RecobrimentoLateralOndaMm(TelhaTp40 telha, int ondas = 2)
        {
            if (telha == null) return 0.0;
            if (ondas < 0) ondas = 0;
            return telha.PassoOndaMm.Valor * (double)ondas;
        }

        /// <summary>
        /// Passo de avanço de cada coluna da telha (u): LarguraUtilMm menos recobrimento lateral
        /// de 2 ondas. Corresponde ao avanço para o encaixe lateral (side-lap).
        /// </summary>
        public static double PassoAvanco(TelhaTp40 telha)
        {
            if (telha == null) return 0.0;
            double passo = telha.PassoOndaMm.Valor;
            double rec = 2.0 * passo;
            double avanco = telha.LarguraUtilMm.Valor - rec;
            if (avanco < passo / 2.0) avanco = telha.LarguraUtilMm.Valor;
            return avanco;
        }

        /// <summary>
        /// Numero de chapas para cobrir o retangulo envolvente: colunas pelo passo de avanço
        /// (encaixe lateral) e linhas pelo comprimento maximo comercial (longitudinal).
        /// </summary>
        public static int ContagemChapas(double extensaoUMm, double extensaoVMm, TelhaTp40 telha)
        {
            if (telha == null) throw new ArgumentNullException(nameof(telha));

            double passoAvanco = PassoAvanco(telha);
            double comprimento = telha.ComprimentoMaximoMm.Valor;
            if (passoAvanco <= 1e-6 || comprimento <= 1e-6) return 0;
            if (extensaoUMm <= 1e-6 || extensaoVMm <= 1e-6) return 0;

            int colunas = (int)Math.Ceiling(extensaoUMm / passoAvanco);
            if (colunas < 1) colunas = 1;
            int linhas = (int)Math.Ceiling(extensaoVMm / comprimento);
            return colunas * linhas;
        }

        private static void Adicionar(List<Ponto2DMm> pontos, double u, double z)
        {
            if (pontos.Count > 0 && Math.Abs(pontos[pontos.Count - 1].XMm - u) < 1e-9
                && Math.Abs(pontos[pontos.Count - 1].YMm - z) < 1e-9)
            {
                return;
            }

            pontos.Add(new Ponto2DMm(u, z));
        }
    }
}
