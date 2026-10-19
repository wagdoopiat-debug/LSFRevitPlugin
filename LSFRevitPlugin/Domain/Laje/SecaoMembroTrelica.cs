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
            if (membro == null) throw new ArgumentNullException(nameof(membro));

            bool banzo = membro.Tipo == MembroTrelicaTipo.BanzoSuperior
                || membro.Tipo == MembroTrelicaTipo.BanzoInferior;

            if (banzo && montagem == MontagemTrelica.Encaixada)
            {
                // Banzo-guia: alma horizontal (ao longo da normal), abas verticais, abertura
                // voltada para o interior da trelica.
                bool abreParaCima = membro.Tipo == MembroTrelicaTipo.BanzoInferior;
                return new OrientacaoSecaoMembro(NormalLocal, abreParaCima);
            }

            // Banzo BackToBack e todos os membros de alma: dS = dE x normal.
            Vetor3D dE = DirecaoLocal(membro);
            Vetor3D dS = dE.ProdutoVetorial(NormalLocal).Normalizado();
            if (dS.Comprimento <= 1e-12)
            {
                // Membro degenerado (comprimento ~0): cai na vertical para nao gerar plano invalido.
                dS = new Vetor3D(0.0, 0.0, 1.0);
            }

            return new OrientacaoSecaoMembro(dS, true);
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
