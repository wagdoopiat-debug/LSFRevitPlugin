using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Boards;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Encontros entre paredes vistos pelas placas: canto em L e T (nas pontas da parede) e
    /// cruzamento em + e T visto pela parede que passa (no meio da parede). Tudo calculado a
    /// partir das linhas de localizacao.
    /// </summary>
    public static class EncontrosPlacas
    {
        // Mesma tolerancia da geracao da estrutura para considerar dois pontos o mesmo
        // (0,05 pe, cerca de 1,5 cm).
        private const double ToleranciaInterna = 0.05;

        // Paredes quase paralelas nao formam encontro.
        private const double LimiteParalelo = 0.1;

        private sealed class EncontroNaPonta
        {
            public TipoEncontroNaPonta Tipo;
            public LeituraParede Outra;
            public XYZ Afastando;
        }

        /// <summary>true se a ponta da parede tem um canto em L ou um T.</summary>
        public static bool TemEncontroNaPonta(LeituraParede parede, IList<LeituraParede> todas, bool noInicio)
        {
            return ProcurarNaPonta(parede, todas, noInicio) != null;
        }

        /// <summary>
        /// Onde a placa de uma face termina numa ponta da parede, em mm ao longo da parede a
        /// partir da ponta inicial (pode ser negativo ou maior que o comprimento).
        /// </summary>
        public static double PosicaoDoExtremo(
            LeituraParede parede, IList<LeituraParede> todas, bool noInicio,
            int numeroFace, double espessuraPlacaMm)
        {
            EncontroNaPonta encontro = ProcurarNaPonta(parede, todas, noInicio);

            TipoEncontroNaPonta tipo = TipoEncontroNaPonta.Nenhum;
            bool faceInterna = false;
            double espessuraOutraMm = 0.0;

            if (encontro != null)
            {
                tipo = encontro.Tipo;
                espessuraOutraMm = EspessuraDaEstrutura(encontro.Outra);

                if (tipo == TipoEncontroNaPonta.EmL)
                {
                    // A face fica do lado de dentro do canto se a outra parede segue desse lado.
                    bool voltadaParaDentro = encontro.Afastando.DotProduct(parede.Normal) > 0;
                    faceInterna = numeroFace == 1 ? voltadaParaDentro : !voltadaParaDentro;
                }
            }

            double estruturaMm = noInicio ? parede.EstruturaUMinMm : parede.EstruturaUMaxMm;

            return ExtremoDaPlaca.Calcular(
                parede.ComprimentoMm, estruturaMm, noInicio, tipo, faceInterna,
                espessuraPlacaMm, espessuraOutraMm);
        }

        /// <summary>
        /// Trechos (U0 a U1, em mm ao longo da parede) onde a placa desta face e interrompida
        /// porque outra parede encosta nela:
        ///   - T: a parede que passa e interrompida so na face do lado de onde a outra chega, e
        ///     fica recuada pela espessura da placa da que chega (a que chega encosta no perfil);
        ///   - +: as duas paredes se interrompem uma a outra, nas duas faces; a de menor Id encosta
        ///     no perfil da outra e a de maior Id fica recuada pela espessura da placa da outra.
        /// O corte e centrado no eixo da outra parede.
        /// </summary>
        public static List<RetanguloMm> Interrupcoes(
            LeituraParede parede, IList<LeituraParede> todas, int numeroFace, double espessuraPlacaMm)
        {
            var lista = new List<RetanguloMm>();
            double comprimento = Interno(parede.ComprimentoMm);

            foreach (LeituraParede outra in todas)
            {
                if (ReferenceEquals(outra, parede) || !outra.TemEstrutura) continue;

                // s: posicao do encontro ao longo desta parede; t: ao longo da outra.
                double s;
                double t;
                if (!Cruzamento(parede, outra, out s, out t)) continue;

                // O encontro tem que estar no meio desta parede (nas pontas e um L ou um T visto pela outra).
                if (s <= ToleranciaInterna || s >= comprimento - ToleranciaInterna) continue;

                double comprimentoOutra = Interno(outra.ComprimentoMm);
                bool cruza = t > ToleranciaInterna && t < comprimentoOutra - ToleranciaInterna;
                bool encostaNoPerfilDaOutra;

                if (cruza)
                {
                    encostaNoPerfilDaOutra = parede.Parede.Id.IntegerValue < outra.Parede.Id.IntegerValue;
                }
                else
                {
                    // T: so conta se uma ponta da outra parede cai aqui.
                    bool pontaInicial = Math.Abs(t) <= ToleranciaInterna;
                    bool pontaFinal = Math.Abs(t - comprimentoOutra) <= ToleranciaInterna;
                    if (!pontaInicial && !pontaFinal) continue;

                    // Direcao em que a outra parede segue a partir do encontro: a face dela e a
                    // do lado para onde ela vai.
                    XYZ sentido = pontaInicial ? outra.Direcao : outra.Direcao.Negate();
                    int faceDoLadoDaOutra = sentido.DotProduct(parede.Normal) > 0 ? 1 : 2;
                    if (faceDoLadoDaOutra != numeroFace) continue;

                    // A parede que chega encosta no perfil desta; esta fica recuada.
                    encostaNoPerfilDaOutra = false;
                }

                double larguraDoCorteMm = ExtremoDaPlaca.LarguraDoCorte(
                    encostaNoPerfilDaOutra, EspessuraDaEstrutura(outra), espessuraPlacaMm);
                double centroMm = ParaMm(s);
                lista.Add(new RetanguloMm(
                    centroMm - larguraDoCorteMm / 2.0, centroMm + larguraDoCorteMm / 2.0, 0.0, 0.0));
            }

            return lista;
        }

        /// <summary>
        /// Procura, numa ponta da parede, um canto em L (a ponta encosta na ponta de outra parede)
        /// ou um T (a ponta cai no meio de outra parede). Ignora paredes sem estrutura gerada.
        /// </summary>
        private static EncontroNaPonta ProcurarNaPonta(
            LeituraParede parede, IList<LeituraParede> todas, bool noInicio)
        {
            XYZ ponto = Ponta(parede, noInicio);
            double alvo = noInicio ? 0.0 : Interno(parede.ComprimentoMm);

            foreach (LeituraParede outra in todas)
            {
                if (ReferenceEquals(outra, parede) || !outra.TemEstrutura) continue;

                // Canto em L: as duas pontas no mesmo lugar, sem estar alinhadas.
                for (int extremo = 0; extremo < 2; extremo++)
                {
                    bool inicioDaOutra = extremo == 0;
                    if (DistanciaPlana(ponto, Ponta(outra, inicioDaOutra)) > ToleranciaInterna) continue;

                    XYZ sentido = inicioDaOutra ? outra.Direcao : outra.Direcao.Negate();
                    if (Math.Abs(sentido.DotProduct(parede.Direcao)) > 0.9) continue;

                    return new EncontroNaPonta
                    {
                        Tipo = TipoEncontroNaPonta.EmL,
                        Outra = outra,
                        Afastando = sentido
                    };
                }

                // T: a ponta desta parede cai no meio da outra.
                double s;
                double t;
                if (!Cruzamento(parede, outra, out s, out t)) continue;

                double comprimentoOutra = Interno(outra.ComprimentoMm);
                if (Math.Abs(s - alvo) <= ToleranciaInterna
                    && t > ToleranciaInterna && t < comprimentoOutra - ToleranciaInterna)
                {
                    return new EncontroNaPonta { Tipo = TipoEncontroNaPonta.EmT, Outra = outra };
                }
            }

            return null;
        }

        /// <summary>
        /// Onde as linhas de localizacao das duas paredes se cruzam: s = distancia ao longo de a e
        /// t = ao longo de b, ambas a partir da ponta inicial, em unidades internas. false se
        /// forem paralelas.
        /// </summary>
        private static bool Cruzamento(LeituraParede a, LeituraParede b, out double s, out double t)
        {
            s = 0.0;
            t = 0.0;

            double cruz = a.Direcao.X * b.Direcao.Y - a.Direcao.Y * b.Direcao.X;
            if (Math.Abs(cruz) < LimiteParalelo) return false;

            double wx = b.Inicio.X - a.Inicio.X;
            double wy = b.Inicio.Y - a.Inicio.Y;

            s = (wx * b.Direcao.Y - wy * b.Direcao.X) / cruz;
            t = (wx * a.Direcao.Y - wy * a.Direcao.X) / cruz;
            return true;
        }

        private static double EspessuraDaEstrutura(LeituraParede parede)
        {
            return parede.EstruturaNMaxMm - parede.EstruturaNMinMm;
        }

        private static XYZ Ponta(LeituraParede parede, bool noInicio)
        {
            if (noInicio) return parede.Inicio;

            return parede.Inicio + parede.Direcao.Multiply(Interno(parede.ComprimentoMm));
        }

        private static double DistanciaPlana(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Interno(double mm)
        {
            return RevitVersionAdapter.MilimetrosParaInterno(mm);
        }

        private static double ParaMm(double valorInterno)
        {
            return RevitVersionAdapter.InternoParaCentimetros(valorInterno) * 10.0;
        }
    }
}