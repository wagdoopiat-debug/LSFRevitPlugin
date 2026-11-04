using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tipo do membro que corre na borda da laje (nas linhas de apoio, onde as vigas terminam).
    /// Para vigas convencionais a borda e a guia "U" (travamento leve). Para vigas trelicadas a
    /// borda passa a ser uma trelica de travamento, que recebe as pontas das trelicas nos dois
    /// apoios -- nao se aplica a guia "U" como se a estrutura fosse de vigas convencionais.
    /// </summary>
    public enum TipoMembroBorda
    {
        /// <summary>Guia "U" de borda (vigas convencionais: perfil simples, caixao, back-to-back).</summary>
        GuiaBorda,

        /// <summary>Trelica transversal de travamento (vigas trelicadas).</summary>
        TrelicaTravamento
    }

    /// <summary>
    /// Regra pura do travamento das trelicas da laje. NAO dimensiona nada: apenas decide o tipo
    /// do membro de borda e identifica as linhas de apoio (as duas bordas do envolvente) onde o
    /// travamento encontra as trelicas. A regra de quantidade/posicao de travamentos entre
    /// trelicas e das fitas tensionadas e geometrica de partida, NAO normativa (ver FitasTensionadasLaje).
    /// </summary>
    public static class TravamentoLaje
    {
        /// <summary>
        /// Membro que corre na borda da laje, conforme o tipo de viga escolhido.
        /// Trelica -> trelica de travamento; demais tipos -> guia "U".
        /// </summary>
        public static TipoMembroBorda MembroDeBorda(TipoVigaLaje tipoViga)
        {
            return tipoViga == TipoVigaLaje.Trelica
                ? TipoMembroBorda.TrelicaTravamento
                : TipoMembroBorda.GuiaBorda;
        }

        /// <summary>
        /// Posicoes u (mm) das linhas de apoio da laje no envolvente orientado: as duas bordas
        /// (inicio e fim do vao), onde as vigas/trelicas terminam e recebem o travamento. Lista
        /// vazia se a orientacao for nula.
        /// </summary>
        public static List<double> Apoios(OrientacaoVigas orientacao)
        {
            var apoios = new List<double>();
            if (orientacao == null) return apoios;

            apoios.Add(orientacao.UMinMm);
            apoios.Add(orientacao.UMaxMm);
            return apoios;
        }

        /// <summary>Numero de pecas de cada conjunto de travamento (quadrado + diagonal).</summary>
        public const int PecasPorConjunto = 5;

        /// <summary>
        /// Conjuntos de travamento dos dois apoios. Cada conjunto fica no vao livre entre duas
        /// trelicas adjacentes e tem exatamente cinco pecas: dois banzos, dois montantes e uma
        /// diagonal. A lista contem um conjunto por par de trelicas em cada apoio, sem repetir
        /// a mesma interface. As posicoes v dos membros ja consideram o recuo da meia largura
        /// transversal das trelicas, inclusive o recuo das trelicas de borda.
        /// </summary>
        public static List<(double UApoioMm, double VInicioLivreMm, double VFimLivreMm, List<MembroTrelica> Membros)>
            CalcularConjuntos(
                OrientacaoVigas orientacao,
                ResultadoLayoutVigas layout,
                double alturaTrelicaMm,
                double larguraTransversalTrelicaMm)
        {
            return CalcularConjuntos(
                orientacao, layout, alturaTrelicaMm, larguraTransversalTrelicaMm, 0.0);
        }

        /// <summary>
        /// Variante com o recuo dos montantes para dentro do conjunto. O recuo deve ser a metade
        /// da dimensao transversal real do montante de travamento, para que ele nasça na face da
        /// trelica e cresca para o vao livre, sem invadir a trelica adjacente.
        /// </summary>
        public static List<(double UApoioMm, double VInicioLivreMm, double VFimLivreMm, List<MembroTrelica> Membros)>
            CalcularConjuntos(
                OrientacaoVigas orientacao,
                ResultadoLayoutVigas layout,
                double alturaTrelicaMm,
                double larguraTransversalTrelicaMm,
                double recuoMontantesMm)
        {
            var conjuntos = new List<(double, double, double, List<MembroTrelica>)>();
            if (orientacao == null || layout == null || layout.Vigas.Count < 2) return conjuntos;
            if (double.IsNaN(alturaTrelicaMm) || double.IsInfinity(alturaTrelicaMm)
                || alturaTrelicaMm <= 0.0)
            {
                return conjuntos;
            }

            double largura = double.IsNaN(larguraTransversalTrelicaMm)
                || double.IsInfinity(larguraTransversalTrelicaMm)
                || larguraTransversalTrelicaMm < 0.0
                ? 0.0
                : larguraTransversalTrelicaMm;
            double meiaLargura = largura / 2.0;

            // Centros efetivos: usa a mesma posicao fisica da geometria longitudinal
            // (LajeGenerationService.CriarVigas), inclusive o recuo de borda.
            var centros = new List<double>();
            foreach (VigaLaje viga in layout.Vigas)
            {
                centros.Add(LayoutVigas.PosicaoFisicaTransversalMm(
                    orientacao, viga.PosicaoTransversalMm, largura));
            }

            centros.Sort();

            foreach (double uApoio in Apoios(orientacao))
            {
                for (int i = 0; i + 1 < centros.Count; i++)
                {
                    double vInicio = centros[i];
                    double vFim = centros[i + 1];
                    if (vFim - vInicio <= LayoutVigas.ToleranciaMm) continue;

                    double vLivreInicio = vInicio + meiaLargura;
                    double vLivreFim = vFim - meiaLargura;
                    if (vLivreFim - vLivreInicio <= LayoutVigas.ToleranciaMm) continue;

                    List<MembroTrelica> membros =
                        MembrosConjunto(vLivreInicio, vLivreFim, alturaTrelicaMm, recuoMontantesMm);
                    if (membros.Count != PecasPorConjunto) continue;

                    conjuntos.Add((uApoio, vLivreInicio, vLivreFim, membros));
                }
            }

            return conjuntos;
        }

        /// <summary>
        /// Quatro pecas formando o quadrado (dois banzos e dois montantes) e uma diagonal
        /// central. Coordenadas em v (mm) e z relativo ao centro da trelica. Os montantes sao
        /// recuados para dentro do conjunto por <paramref name="recuoMontantesMm"/>, de modo que
        /// suas faces externas coincidam com as faces das trelicas adjacentes.
        /// </summary>
        public static List<MembroTrelica> MembrosConjunto(
            double vLivreInicioMm, double vLivreFimMm, double alturaTrelicaMm)
        {
            return MembrosConjunto(vLivreInicioMm, vLivreFimMm, alturaTrelicaMm, 0.0);
        }

        /// <summary>Variante com recuo dos montantes para dentro do conjunto.</summary>
        public static List<MembroTrelica> MembrosConjunto(
            double vLivreInicioMm, double vLivreFimMm, double alturaTrelicaMm,
            double recuoMontantesMm)
        {
            if (double.IsNaN(alturaTrelicaMm) || double.IsInfinity(alturaTrelicaMm)
                || alturaTrelicaMm <= 0.0
                || vLivreFimMm - vLivreInicioMm <= LayoutVigas.ToleranciaMm)
            {
                return new List<MembroTrelica>();
            }

            double comprimento = vLivreFimMm - vLivreInicioMm;
            double zTopo = alturaTrelicaMm / 2.0;
            double zBase = -alturaTrelicaMm / 2.0;

            double recuo = double.IsNaN(recuoMontantesMm) || double.IsInfinity(recuoMontantesMm)
                || recuoMontantesMm <= 0.0
                ? 0.0
                : recuoMontantesMm;

            // Nao deixa o recuo consumir o vao livre nem cruzar os dois montantes.
            double limite = comprimento / 2.0 - LayoutVigas.ToleranciaMm;
            if (limite < 0.0) limite = 0.0;
            if (recuo > limite) recuo = limite;

            double uMontanteInicio = recuo;
            double uMontanteFim = comprimento - recuo;

            return new List<MembroTrelica>
            {
                new MembroTrelica(
                    MembroTrelicaTipo.BanzoInferior,
                    0.0, zBase, comprimento, zBase),
                new MembroTrelica(
                    MembroTrelicaTipo.BanzoSuperior,
                    0.0, zTopo, comprimento, zTopo),
                new MembroTrelica(
                    MembroTrelicaTipo.Montante,
                    uMontanteInicio, zBase, uMontanteInicio, zTopo),
                new MembroTrelica(
                    MembroTrelicaTipo.Montante,
                    uMontanteFim, zBase, uMontanteFim, zTopo),
                new MembroTrelica(
                    MembroTrelicaTipo.Diagonal,
                    0.0, zBase, comprimento, zTopo)
            };
        }

        /// <summary>
        /// Montagem geometrica dos travamentos dos apoios. Eles usam sempre Encaixada para que
        /// os montantes crescam para dentro do vao livre, independentemente da montagem das
        /// trelicas principais.
        /// </summary>
        public static MontagemTrelica MontagemTravamentoApoio(MontagemTrelica montagemPrincipal)
        {
            return MontagemTrelica.Encaixada;
        }

        /// <summary>
        /// Metade da dimensao transversal real do montante de travamento, usada para recua-lo
        /// para dentro do conjunto. Encaixada: a largura do montante corre ao longo de v e vale
        /// AbaMm. FaceComFace: a profundidade da alma corre ao longo de v e vale LarguraAlmaMm.
        /// </summary>
        public static double RecuoMontanteParaDentroMm(
            MontagemTrelica montagem, double abaMm, double larguraAlmaMm)
        {
            double dimensao = montagem == MontagemTrelica.FaceComFace ? larguraAlmaMm : abaMm;
            if (double.IsNaN(dimensao) || double.IsInfinity(dimensao) || dimensao <= 0.0)
                return 0.0;

            return dimensao / 2.0;
        }
    }
}
