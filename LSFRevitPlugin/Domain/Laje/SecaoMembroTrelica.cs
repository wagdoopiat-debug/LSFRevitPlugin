using System;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Vetor 3D puro (sem Revit) usado para descrever direcoes da secao de um membro. Nao
    /// representa posicao, apenas orientacao; componentes em qualquer unidade consistente.
    /// </summary>
    public readonly struct Vetor3D
    {
        public Vetor3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double Comprimento => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vetor3D Normalizado()
        {
            double c = Comprimento;
            if (c <= 1e-12) return new Vetor3D(0.0, 0.0, 0.0);
            return new Vetor3D(X / c, Y / c, Z / c);
        }

        /// <summary>Produto vetorial (regra da mao direita).</summary>
        public Vetor3D ProdutoVetorial(Vetor3D outro)
        {
            return new Vetor3D(
                Y * outro.Z - Z * outro.Y,
                Z * outro.X - X * outro.Z,
                X * outro.Y - Y * outro.X);
        }

        public double ProdutoEscalar(Vetor3D outro)
        {
            return X * outro.X + Y * outro.Y + Z * outro.Z;
        }
    }

    /// <summary>
    /// Orientacao da secao transversal de uma barra da trelica, expressa nas coordenadas LOCAIS
    /// da trelica: x ao longo do vao (u), y na normal horizontal do plano (para fora do plano) e
    /// z vertical (para cima). Os eixos locais formam um referencial ortogonal direito, o mesmo
    /// usado para mapear para o mundo (uDir, normal, BasisZ).
    /// </summary>
    public sealed class OrientacaoSecaoMembro
    {
        public OrientacaoSecaoMembro(Vetor3D direcaoSecaoPerp, bool abertoParaFrente)
        {
            DirecaoSecaoPerp = direcaoSecaoPerp;
            AbertoParaFrente = abertoParaFrente;
        }

        /// <summary>
        /// Eixo da secao que carrega a ALMA (profundidade). O outro eixo da secao e o eixo de
        /// extrusao cruzado com este (dE x dS) e carrega a aba (largura).
        /// </summary>
        public Vetor3D DirecaoSecaoPerp { get; }

        /// <summary>
        /// Se a abertura do "C" aponta para o lado positivo do eixo da aba (dV) para a secao base
        /// (sem espelhamento). O espelhamento por sub-perfil inverte este valor.
        /// </summary>
        public bool AbertoParaFrente { get; }
    }

    /// <summary>
    /// Regra pura da orientacao da secao dos membros da trelica. Centraliza a decisao geometrica
    /// para que a geometria (TrelicaBuilder) e os testes usem a MESMA logica, sem Revit.
    ///
    /// Regras (correcao geometrica):
    /// - Membros de alma (montantes e diagonais), em QUALQUER montagem, e banzos em BackToBack:
    ///   dirSecaoPerp = dE x normal. A alma fica no plano da trelica e o deslocamento transversal
    ///   (dV = dE x dirSecaoPerp) aponta uniformemente para -normal, de modo que o BackToBack
    ///   empilha os dois "C" para fora do plano (costas com costas) em todas as pecas.
    /// - Banzos em Encaixada: dirSecaoPerp = normal. A alma fica HORIZONTAL e as abas VERTICAIS,
    ///   com a abertura voltada para o interior da trelica (banzo inferior abre para cima,
    ///   superior para baixo), formando um canal/guia onde os membros de alma (largura fora do
    ///   plano = aba) se encaixam.
    /// - Em FaceComFace, alem da orientacao, a ALMA REAL do perfil C (que dista Aba/2 do eixo da
    ///   secao) precisa ser levada ao plano da trelica: e o que <see cref="DeslocamentoAlmaMm"/>
    ///   calcula. Sem esse deslocamento os eixos coincidem, mas as almas ficam em planos paralelos
    ///   separados por Aba e os corpos dos membros interpenetram nos nos.
    /// </summary>
    public static class SecaoMembroTrelica
    {
        /// <summary>Normal horizontal do plano da trelica em coordenadas locais (aponta para +y).</summary>
        public static readonly Vetor3D NormalLocal = new Vetor3D(0.0, 1.0, 0.0);

        /// <summary>
        /// Orientacao da secao para um membro, dada a montagem construtiva. Lanca
        /// ArgumentNullException se o membro for nulo.
        /// </summary>
        public static OrientacaoSecaoMembro Calcular(MembroTrelica membro, MontagemTrelica montagem)
        {
            return Calcular(membro, montagem, NormalLocal);
        }

        /// <summary>
        /// Variante usada quando o plano da trelica e espelhado (por exemplo, a trelica de
        /// extremidade que precisa abrir os banzos para fora). O normal local continua sendo o
        /// eixo y do referencial da trelica; apenas o seu sentido no mundo e invertido.
        /// </summary>
        public static OrientacaoSecaoMembro Calcular(
            MembroTrelica membro, MontagemTrelica montagem, Vetor3D normalLocal)
        {
            if (membro == null) throw new ArgumentNullException(nameof(membro));

            bool banzo = membro.Tipo == MembroTrelicaTipo.BanzoSuperior
                || membro.Tipo == MembroTrelicaTipo.BanzoInferior;

            // Em Encaixada, somente os banzos usam a seção como canal-guia.
            if (banzo && montagem == MontagemTrelica.Encaixada)
            {
                bool abreParaCima = membro.Tipo == MembroTrelicaTipo.BanzoInferior;
                return new OrientacaoSecaoMembro(normalLocal, abreParaCima);
            }
            // Encaixada: membros de alma com orientacao transversal.
            // Face com Face: alma no plano da trelica.
            Vetor3D dS;

            if (montagem == MontagemTrelica.Encaixada)
            {
                dS = normalLocal;
            }
            else
            {
                Vetor3D dE = DirecaoLocal(membro);
                dS = dE.ProdutoVetorial(normalLocal).Normalizado();

                if (dS.Comprimento <= 1e-12)
                    dS = new Vetor3D(0.0, 0.0, 1.0);
            }

            bool abertoParaFrente = banzo
                || montagem != MontagemTrelica.FaceComFace;

            return new OrientacaoSecaoMembro(dS, abertoParaFrente);
        }

        /// <summary>
        /// Sentido do eixo normal do plano da trelica (v) para que os banzos fiquem voltados
        /// para fora da cobertura. Em FaceComFace a abertura do "C" aponta para -normal; assim,
        /// a borda de vMin usa +normal e a de vMax usa -normal. A montagem Encaixada mantem o
        /// sentido original (comportamento ja aceito).
        /// </summary>
        public static double SinalNormalPlanoTransversal(
            MontagemTrelica montagem, bool naBordaMin, bool naBordaMax)
        {
            if (montagem != MontagemTrelica.FaceComFace) return 1.0;
            return naBordaMax && !naBordaMin ? -1.0 : 1.0;
        }

        /// <summary>
        /// Deslocamento transversal (na direcao da aba, dV = dE x dS) que coloca a face externa
        /// da ALMA do perfil C sobre o plano da trelica. A secao e modelada centrada no eixo do
        /// membro, mas a alma fica na borda do "C", a Aba/2 do centro; por isso, em FaceComFace,
        /// o perfil precisa ser deslocado de +Aba/2 (abertura para +dV) ou -Aba/2 (abertura para
        /// -dV) para que a alma -- e nao o centro geometrico -- caia no no. O resultado e um unico
        /// perfil C por barra, com as almas dos banzos e dos membros de alma em contato face a
        /// face e as abas apontando para lados opostos. Em Encaixada o perfil permanece centrado
        /// (deslocamento zero), preservando o encaixe nos canais-guia.
        /// </summary>
        /// <param name="montagem">Montagem construtiva da trelica.</param>
        /// <param name="abertoParaFrente">Sentido real de abertura do "C" (ja considerando espelhamento).</param>
        /// <param name="abaMm">Largura da aba do perfil, em milimetros.</param>
        public static double DeslocamentoAlmaMm(
            MontagemTrelica montagem, bool abertoParaFrente, double abaMm)
        {
            if (montagem != MontagemTrelica.FaceComFace) return 0.0;
            if (double.IsNaN(abaMm) || double.IsInfinity(abaMm) || abaMm <= 0.0) return 0.0;

            return (abertoParaFrente ? 0.5 : -0.5) * abaMm;
        }

        /// <summary>Direcao unitaria do membro no plano da trelica (x = u, z = vertical).</summary>
        public static Vetor3D DirecaoLocal(MembroTrelica membro)
        {
            if (membro == null) throw new ArgumentNullException(nameof(membro));
            double du = membro.FimUMm - membro.InicioUMm;
            double dz = membro.FimZMm - membro.InicioZMm;
            double comprimento = Math.Sqrt(du * du + dz * dz);
            if (comprimento <= 1e-12) return new Vetor3D(0.0, 0.0, 0.0);
            return new Vetor3D(du / comprimento, 0.0, dz / comprimento);
        }
    }
}

