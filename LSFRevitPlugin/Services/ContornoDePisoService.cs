using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Extrai o contorno de uma laje a partir de um PISO existente do Revit, sem modificar o
    /// piso. Le a face superior, converte as arestas retas em pontos em mm e monta o contorno.
    ///
    /// Escopo v1: contorno simples (so o laco externo). Furos/lacos internos sao contados e
    /// reportados como aviso, mas nao entram na geracao. Curvas nao retas (arcos) impedem a
    /// leitura do laco e viram erro, sem tentar aproximar.
    /// </summary>
    public static class ContornoDePisoService
    {
        public static ResultadoContornoLaje Extrair(Document doc, Floor piso)
        {
            var resultado = new ResultadoContornoLaje { Origem = TipoOrigemContorno.PisoExistente };

            if (doc == null)
            {
                resultado.Erros.Add("Documento nao informado.");
                return resultado;
            }

            if (piso == null)
            {
                resultado.Erros.Add("Piso nao informado.");
                return resultado;
            }

            resultado.ElementoOrigem = piso.Id;

            PlanarFace face = ObterFaceSuperior(doc, piso);
            if (face == null)
            {
                resultado.Erros.Add("Nao foi possivel obter a face superior do piso selecionado.");
                return resultado;
            }

            IList<CurveLoop> lacos;
            try
            {
                lacos = face.GetEdgesAsCurveLoops();
            }
            catch (Exception ex)
            {
                resultado.Erros.Add("Falha ao ler as bordas do piso: " + ex.Message);
                return resultado;
            }

            var candidatos = new List<List<Ponto2DMm>>();
            bool tinhaCurvaNaoReta = false;

            foreach (CurveLoop laco in lacos)
            {
                List<Ponto2DMm> pontos = PontosDoLaco(laco, out bool reto);
                if (!reto)
                {
                    tinhaCurvaNaoReta = true;
                    continue;
                }

                if (pontos.Count >= 3) candidatos.Add(pontos);
            }

            if (candidatos.Count == 0)
            {
                if (tinhaCurvaNaoReta)
                {
                    resultado.Erros.Add(
                        "O contorno do piso tem curvas (arcos/circulos); a v1 aceita somente bordas retas.");
                }
                else
                {
                    resultado.Erros.Add("O piso selecionado nao tem um contorno fechado valido.");
                }

                return resultado;
            }

            // O contorno externo e o laco de maior area; os demais sao furos.
            int indiceExterno = 0;
            double maiorArea = Math.Abs(AreaShoelace(candidatos[0]));
            for (int i = 1; i < candidatos.Count; i++)
            {
                double area = Math.Abs(AreaShoelace(candidatos[i]));
                if (area > maiorArea)
                {
                    maiorArea = area;
                    indiceExterno = i;
                }
            }

            resultado.QuantidadeFuros = candidatos.Count - 1;
            if (resultado.QuantidadeFuros > 0)
            {
                resultado.Avisos.Add(
                    "O piso tem " + resultado.QuantidadeFuros
                    + (resultado.QuantidadeFuros == 1 ? " furo/abertura" : " furos/aberturas")
                    + "; a v1 considera apenas o contorno externo.");
            }

            if (tinhaCurvaNaoReta)
            {
                resultado.Avisos.Add(
                    "Algumas bordas internas curvas foram ignoradas (a v1 aceita somente bordas retas).");
            }

            var contorno = new ContornoLaje(candidatos[indiceExterno]);
            List<string> errosContorno = contorno.Validar();
            if (errosContorno.Count > 0)
            {
                resultado.Erros.AddRange(errosContorno);
                return resultado;
            }

            resultado.Contorno = contorno;
            resultado.CotaMm = Core.RevitVersionAdapter.InternoParaMilimetros(face.Origin.Z);
            return resultado;
        }

        private static PlanarFace ObterFaceSuperior(Document doc, Floor piso)
        {
            IList<Reference> faces = HostObjectUtils.GetTopFaces(piso);
            foreach (Reference referencia in faces)
            {
                Element elemento = doc.GetElement(referencia);
                if (elemento == null) continue;

                if (elemento.GetGeometryObjectFromReference(referencia) is PlanarFace face)
                {
                    return face;
                }
            }

            return null;
        }

        private static List<Ponto2DMm> PontosDoLaco(CurveLoop laco, out bool reto)
        {
            var pontos = new List<Ponto2DMm>();
            reto = true;

            foreach (Curve curva in laco)
            {
                if (!(curva is Line linha))
                {
                    reto = false;
                    return pontos;
                }

                // Apenas o ponto inicial de cada linha: os lacos sao encadeados, entao o inicio
                // de uma aresta e o fim da anterior; o ponto de fechamento se repete e a
                // ContornoLaje.remove.
                XYZ inicio = linha.GetEndPoint(0);
                pontos.Add(new Ponto2DMm(
                    Core.RevitVersionAdapter.InternoParaMilimetros(inicio.X),
                    Core.RevitVersionAdapter.InternoParaMilimetros(inicio.Y)));
            }

            return pontos;
        }

        private static double AreaShoelace(IReadOnlyList<Ponto2DMm> pts)
        {
            double soma = 0.0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Ponto2DMm a = pts[i];
                Ponto2DMm b = pts[(i + 1) % n];
                soma += a.XMm * b.YMm - b.XMm * a.YMm;
            }

            return soma / 2.0;
        }
    }
}
