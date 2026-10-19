using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>Papel de uma faixa modelada sobre a telha (a propria chapa ou o enchimento).</summary>
    public enum PapelFaixa
    {
        Telha,
        Enchimento
    }

    /// <summary>
    /// Faixa (prisma) do perfil da telha varrida ao longo de v, ja recortada pelo contorno da
    /// laje na varredura em u. Coordenadas locais: A/B na linha de fundo da onda; D/C na linha de
    /// topo (fundo + espessura para a chapa; altura da onda para o enchimento). O quadrado A-B-C-D
    /// fica anti-horario em (u, z). VInicio/VFim delimitam a faixa ao longo da viga (eixo v).
    /// </summary>
    public sealed class FaixaPerfil
    {
        public FaixaPerfil(
            Ponto2DMm a, Ponto2DMm b, Ponto2DMm c, Ponto2DMm d,
            double vInicioMm, double vFimMm, int coluna, int segmento, PapelFaixa papel)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            VInicioMm = vInicioMm;
            VFimMm = vFimMm;
            Coluna = coluna;
            Segmento = segmento;
            Papel = papel;
        }

        /// <summary>Inicio da linha de fundo no u inicial do segmento.</summary>
        public Ponto2DMm A { get; }

        /// <summary>Fim da linha de fundo (u final do segmento).</summary>
        public Ponto2DMm B { get; }

        /// <summary>Fim da linha de topo (u final).</summary>
        public Ponto2DMm C { get; }

        /// <summary>Inicio da linha de topo (u inicial).</summary>
        public Ponto2DMm D { get; }

        public double VInicioMm { get; }

        public double VFimMm { get; }

        /// <summary>Indice da chapa (coluna) ao longo de u.</summary>
        public int Coluna { get; }

        /// <summary>Indice do segmento do perfil (fundo ou rampa).</summary>
        public int Segmento { get; }

        public PapelFaixa Papel { get; }

        public double ComprimentoMm => VFimMm - VInicioMm;

        /// <summary>Area do quadrado A-B-C-D em (u, z), mm2 (sempre positiva).</summary>
        public double AreaMm2
        {
            get
            {
                var quad = new List<Ponto2DMm> { A, B, C, D };
                return RecortePoligono.Area(quad);
            }
        }
    }

    /// <summary>
    /// Resultado do telhamento: as faixas recortadas, os avisos e a contagem de chapas comerciais
    /// (colunas x linhas) conforme o catalogo da telha.
    /// </summary>
    public sealed class ResultadoFaixas
    {
        public List<FaixaPerfil> Faixas { get; } = new List<FaixaPerfil>();

        public List<string> Avisos { get; } = new List<string>();

        public int ChapasTelha { get; set; }

        public double ExtensaoUMm { get; set; }

        public double ExtensaoVMm { get; set; }
    }

    /// <summary>
    /// Gera a geometria 2D da telha trapezoidal TP40 recortada pelo contorno real da laje. A chapa
    /// corre ao longo de u (entre colunas de largura util) e e varrida ao longo de v; em cada
    /// segmento de perfil, o trecho de v e obtido por varredura (RecortePoligono.IntervalosV) do
    /// contorno na coordenada u central do segmento. Puro (sem Revit) para ser testado isolado.
    ///
    /// Limitacoes registradas: a varredura usa o u central do segmento do perfil (aprox.; o
    /// contorno pode variar dentro do segmento) e nao representa furos internos (o contorno nao os
    /// suporta). Cabecalhos/cumeeiras nao sao modelados. Emitir Aviso quando a chapa exceder o
    /// comprimento maximo comercial.
    /// </summary>
    public static class TelhamentoTp40
    {
        /// <summary>Menor altura de faixa de enchimento modelada (mm) para evitar solidos degenerados.</summary>
        public const double AlturaMinimaEnchimentoMm = 0.5;

        /// <summary>Menor comprimento de faixa em v (mm) para modelar a chapa.</summary>
        public const double ComprimentoMinimoMm = 1.0;

        /// <summary>Faixas da chapa da telha (fundo + espessura) recortadas pelo contorno.</summary>
        public static ResultadoFaixas CalcularTelha(
            ContornoLaje contorno, OrientacaoVigas orientacao, TelhaTp40 telha, double espessuraMm)
        {
            if (telha == null) return new ResultadoFaixas();

            double t = SecaoTelhaTp40.EspessuraModelada(telha, espessuraMm);
            return Gerar(contorno, orientacao, telha, PapelFaixa.Telha, (zFundo, _) => zFundo + t);
        }

        /// <summary>
        /// Faixas do enchimento das ondas: a regiao entre a linha de fundo da onda e a crista, ao
        /// longo de todo o vao. Distinto da placa de argamassa acima da onda.
        /// </summary>
        public static ResultadoFaixas CalcularEnchimento(
            ContornoLaje contorno, OrientacaoVigas orientacao, TelhaTp40 telha)
        {
            if (telha == null) return new ResultadoFaixas();

            double altura = telha.AlturaOndaMm.Valor;
            return Gerar(contorno, orientacao, telha, PapelFaixa.Enchimento, (_, __) => altura);
        }

        private static ResultadoFaixas Gerar(
            ContornoLaje contorno,
            OrientacaoVigas orientacao,
            TelhaTp40 telha,
            PapelFaixa papel,
            Func<double, double, double> topo)
        {
            var resultado = new ResultadoFaixas();

            if (contorno == null || orientacao == null)
            {
                resultado.Avisos.Add("Telhamento nao gerado: contorno ou orientacao ausentes.");
                return resultado;
            }

            List<string> erros = telha.Validar();
            if (erros.Count > 0)
            {
                foreach (string e in erros) resultado.Avisos.Add("Telha invalida: " + e);
                return resultado;
            }

            double larguraUtil = telha.LarguraUtilMm.Valor;
            if (larguraUtil <= 1e-6)
            {
                resultado.Avisos.Add("Telhamento nao gerado: largura util da telha invalida.");
                return resultado;
            }

            List<Ponto2DMm> perfil = SecaoTelhaTp40.PerfilInferior(telha);
            if (perfil.Count < 2)
            {
                resultado.Avisos.Add("Telhamento nao gerado: perfil da telha degenerado.");
                return resultado;
            }

            List<Ponto2DMm> janela = ParaUV(contorno, orientacao);
            if (janela.Count < 3)
            {
                resultado.Avisos.Add("Telhamento nao gerado: contorno invalido.");
                return resultado;
            }

            double extensaoU = orientacao.ExtensaoUMm;
            double extensaoV = orientacao.ExtensaoVMm;
            resultado.ExtensaoUMm = extensaoU;
            resultado.ExtensaoVMm = extensaoV;

            if (extensaoU <= 1e-6 || extensaoV <= 1e-6)
            {
                resultado.Avisos.Add("Telhamento nao gerado: extensao do contorno degenerada.");
                return resultado;
            }

            // Aproximacao em colunas: a chapa nao acompanha uma borda inclinada em u; quando o
            // contorno nao e retangular, as faixas param na varredura (borda serrilhada).
            if (!RetornoEhRetangular(janela, orientacao))
            {
                resultado.Avisos.Add(
                    "Contorno nao retangular: a telha e recortada por faixas (borda serrilhada nas "
                    + "laterais em u; conferir em Revit).");
            }

            double passoAvanco = SecaoTelhaTp40.PassoAvanco(telha);
            if (passoAvanco <= 1e-6) passoAvanco = larguraUtil;

            int colunas = (int)Math.Ceiling(extensaoU / passoAvanco);
            if (colunas < 1) colunas = 1;

            double tModelada = SecaoTelhaTp40.EspessuraModelada(telha, 0.43);
            if (papel == PapelFaixa.Telha && tModelada < SecaoTelhaTp40.EspessuraMinimaModeladaMm) tModelada = SecaoTelhaTp40.EspessuraMinimaModeladaMm;

            int segmentos = perfil.Count - 1;
            double elevacaoTotal = 0.0;
            for (int col = 0; col < colunas; col++)
            {
                double uChapa = orientacao.UMinMm + col * passoAvanco;
                elevacaoTotal = col * tModelada;

                for (int seg = 0; seg < segmentos; seg++)
                {
                    Ponto2DMm p0 = perfil[seg];
                    Ponto2DMm p1 = perfil[seg + 1];

                    double u0 = uChapa + p0.XMm;
                    double u1 = uChapa + p1.XMm;
                    double uCentro = (u0 + u1) / 2.0;

                    double zFundo0 = p0.YMm;
                    double zFundo1 = p1.YMm;
                    double zTopo0 = topo(zFundo0, p0.XMm);
                    double zTopo1 = topo(zFundo1, p1.XMm);

                    if (papel == PapelFaixa.Enchimento
                        && Math.Abs(zTopo0 - zFundo0) < AlturaMinimaEnchimentoMm
                        && Math.Abs(zTopo1 - zFundo1) < AlturaMinimaEnchimentoMm)
                    {
                        continue;
                    }

                    foreach ((double vInicio, double vFim) in RecortePoligono.IntervalosV(janela, uCentro))
                    {
                        if (vFim - vInicio < ComprimentoMinimoMm) continue;

                     resultado.Faixas.Add(new FaixaPerfil(
                             new Ponto2DMm(u0, zFundo0 + elevacaoTotal),
                             new Ponto2DMm(u1, zFundo1 + elevacaoTotal),
                             new Ponto2DMm(u1, zTopo1 + elevacaoTotal),
                             new Ponto2DMm(u0, zTopo0 + elevacaoTotal),
                             vInicio, vFim, col, seg, papel));
                    }
                }
            }

            resultado.ChapasTelha = SecaoTelhaTp40.ContagemChapas(extensaoU, extensaoV, telha);

            if (papel == PapelFaixa.Telha && elevacaoTotal > telha.EnchimentoOndasM3PorM2.Valor * 1000.0)
            {
                resultado.Avisos.Add(
                    "Empilhamento lateral acumulado pode ultrapassar o enchimento previsto acima da onda; conferir argamassa.");
            }

            return resultado;
        }

        /// <summary>Contorno do mundo (planta) para o plano (u, v) das vigas.</summary>
        private static List<Ponto2DMm> ParaUV(ContornoLaje contorno, OrientacaoVigas orientacao)
        {
            var lista = new List<Ponto2DMm>(contorno.Pontos.Count);
            foreach (Ponto2DMm p in contorno.Pontos)
            {
                lista.Add(new Ponto2DMm(orientacao.CoordenadaU(p), orientacao.CoordenadaV(p)));
            }

            return lista;
        }

        /// <summary>
        /// true quando o contorno em (u, v) coincide com o retangulo envolvente (tolerancia 1 mm),
        /// ou seja, a chapa retangular nao precisa de recorte lateral serrilhado.
        /// </summary>
        private static bool RetornoEhRetangular(List<Ponto2DMm> janela, OrientacaoVigas orientacao)
        {
            double area = RecortePoligono.Area(janela);
            double areaEnv = orientacao.ExtensaoUMm * orientacao.ExtensaoVMm;
            return Math.Abs(area - areaEnv) <= 1.0;
        }
    }
}
