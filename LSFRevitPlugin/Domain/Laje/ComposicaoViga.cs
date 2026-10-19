using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Montagem construtiva das pecas da trelica (independente do tipo de trelica). Afeta a
    /// geometria: Encaixada gera pecas unicas; BackToBack gera cada peca como dois "C" costas com
    /// costas (banzos e web acompanham a mesma configuracao).
    /// </summary>
    public enum MontagemTrelica
    {
        Encaixada,
        BackToBack
    }

    /// <summary>
    /// Configuracao transversal do caixao. FrenteAFrente = dois "C" com as aberturas opostas
    /// (almas a AbaMm/2 do centro, fechando a caixa, largura = AbaMm). Encaixados = dois "C"
    /// com as aberturas voltadas uma para a outra (almas nas bordas a AbaMm do centro,
    /// abas se encontrando no centro, largura = 2*AbaMm). As duas NAO sao geometricamente
    /// equivalentes.
    /// </summary>
    public enum ComposicaoCaixaoTipo
    {
        FrenteAFrente,
        Encaixados
    }

    /// <summary>
    /// Posicao de um perfil dentro de uma viga composta. O deslocamento e transversal ao eixo
    /// da viga (na horizontal), em milimetros; Espelhado indica que a abertura do "C" aponta para
    /// o lado oposto (usado para montar Back to Back e Caixao).
    /// </summary>
    public sealed class SubPerfilViga
    {
        public SubPerfilViga(double deslocamentoTransversalMm, bool espelhado)
        {
            DeslocamentoTransversalMm = deslocamentoTransversalMm;
            Espelhado = espelhado;
        }

        public double DeslocamentoTransversalMm { get; }
        public bool Espelhado { get; }
    }

    /// <summary>
    /// Descreve como uma viga e montada a partir de um ou mais perfis "C", de forma pura (sem
    /// Revit), para poder ser testada e reaproveitada pela geometria. So descreve disposicao e
    /// orientacao dos perfis -- nao cria nem dimensiona estruturalmente nada.
    /// </summary>
    public static class ComposicaoViga
    {
        /// <summary>Sub-perfis de um tipo de viga, dado o perfil base (caixao na config. padrao).</summary>
        public static List<SubPerfilViga> SubPerfis(TipoVigaLaje tipo, Models.PerfilSpec perfil)
        {
            return SubPerfis(tipo, perfil, ComposicaoCaixaoTipo.FrenteAFrente);
        }

        /// <summary>Sub-perfis de um tipo de viga, escolhendo a configuracao do caixao.</summary>
        public static List<SubPerfilViga> SubPerfis(
            TipoVigaLaje tipo, Models.PerfilSpec perfil, ComposicaoCaixaoTipo caixao)
        {
            var lista = new List<SubPerfilViga>();

            if (tipo == TipoVigaLaje.BackToBack && perfil != null)
            {
                // Dois "C" costas com costas: desloca cada um meia aba (AbaMm/2) para os lados
                // opostos, para que as almas se toquem no eixo e as abas fiquem voltadas para fora.
                double meio = perfil.AbaMm / 2.0;
                lista.Add(new SubPerfilViga(+meio, espelhado: false));
                lista.Add(new SubPerfilViga(-meio, espelhado: true));
                return lista;
            }

            if (tipo == TipoVigaLaje.Caixao && perfil != null)
            {
                if (caixao == ComposicaoCaixaoTipo.Encaixados)
                {
                    // Caixao encaixados: dois "C" com as aberturas voltadas UMA para a OUTRA
                    // (para o centro). As almas ficam nas bordas externas, a AbaMm do centro
                    // (almas separadas por 2*AbaMm) e as pontas das abas se encontram no centro.
                    // Largura = 2 * AbaMm. (Correcao: antes os dois "C" tinham o mesmo sentido e
                    // a largura declarada nao batia com o spano real -- era cortado no layout.)
                    double meio = perfil.AbaMm / 2.0;
                    lista.Add(new SubPerfilViga(-meio, espelhado: false));
                    lista.Add(new SubPerfilViga(+meio, espelhado: true));
                    return lista;
                }

                // Caixao frente a frente: dois "C" com as aberturas opostas (um espelhado), no
                // mesmo eixo central. A largura transversal e a aba (almas a AbaMm/2 do centro).
                lista.Add(new SubPerfilViga(0.0, espelhado: false));
                lista.Add(new SubPerfilViga(0.0, espelhado: true));
                return lista;
            }

            lista.Add(new SubPerfilViga(0.0, espelhado: false));
            return lista;
        }

        /// <summary>
        /// Sub-perfis de uma peca da trelica conforme a montagem escolhida: Encaixada gera uma
        /// peca unica; BackToBack gera duas em costas-com-costas (almas juntas, abas para fora).
        /// </summary>
        public static List<SubPerfilViga> SubPerfisMontagem(MontagemTrelica montagem, Models.PerfilSpec perfil)
        {
            var lista = new List<SubPerfilViga>();

            if (montagem == MontagemTrelica.BackToBack && perfil != null)
            {
                double meio = perfil.AbaMm / 2.0;
                lista.Add(new SubPerfilViga(+meio, espelhado: false));
                lista.Add(new SubPerfilViga(-meio, espelhado: true));
                return lista;
            }

            lista.Add(new SubPerfilViga(0.0, espelhado: false));
            return lista;
        }

        /// <summary>Largura transversal total ocupada pela viga (mm), para alinhamento e recuos.</summary>
        public static double LarguraTransversalMm(TipoVigaLaje tipo, Models.PerfilSpec perfil)
        {
            return LarguraTransversalMm(
                tipo, perfil, MontagemTrelica.Encaixada, ComposicaoCaixaoTipo.FrenteAFrente);
        }

        /// <summary>Largura transversal total, considerando tambem a montagem da trelica.</summary>
        public static double LarguraTransversalMm(
            TipoVigaLaje tipo, Models.PerfilSpec perfil, MontagemTrelica montagem)
        {
            return LarguraTransversalMm(tipo, perfil, montagem, ComposicaoCaixaoTipo.FrenteAFrente);
        }

        /// <summary>
        /// Largura transversal total (mm) em funcao de todas as decisoes geometricas: tipo de
        /// viga, montagem da trelica e composicao do caixao. Fonte unica -- os call sites de
        /// layout/recuo DEVEM usar esta sobrecarga para nao divergirem dos solidos gerados.
        /// </summary>
        public static double LarguraTransversalMm(
            TipoVigaLaje tipo, Models.PerfilSpec perfil, MontagemTrelica montagem,
            ComposicaoCaixaoTipo caixao)
        {
            if (perfil == null) return 0.0;
            if (tipo == TipoVigaLaje.BackToBack) return perfil.AbaMm * 2.0;
            if (tipo == TipoVigaLaje.Trelica && montagem == MontagemTrelica.BackToBack)
                return perfil.AbaMm * 2.0;
            if (tipo == TipoVigaLaje.Caixao && caixao == ComposicaoCaixaoTipo.Encaixados)
                return perfil.AbaMm * 2.0;
            return perfil.AbaMm;
        }
    }
}
