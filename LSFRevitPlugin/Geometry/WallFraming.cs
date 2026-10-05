using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Responsavel pela distribuicao linear de montantes ao longo do eixo de uma parede.
    /// Nao trata nos (L/T/+) nem vaos de portas/janelas -- isso fica em WallJunctions.cs
    /// e OpeningReinforcement.cs, respectivamente (fases seguintes).
    /// </summary>
    public class WallFraming
    {
        /// <summary>
        /// Calcula os pontos cartesianos de insercao dos montantes ao longo do eixo da parede,
        /// espacados uniformemente, com fixacao garantida nas duas extremidades. Cada ponto vem
        /// acompanhado de uma flag indicando se e um montante de extremidade (para ajuste de
        /// alinhamento com a face da parede, em vez de centralizacao).
        /// </summary>
        /// <param name="wall">Parede selecionada no Revit.</param>
        /// <param name="espacamentoCm">Espacamento nominal entre montantes, em centimetros (ex: 40 ou 60).</param>
        public List<(XYZ Ponto, bool EhExtremidade)> CalcularPontosMontantes(Wall wall, double espacamentoCm)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall), "Parede nao pode ser nula.");

            LocationCurve locationCurve = wall.Location as LocationCurve;
            if (locationCurve == null)
                throw new InvalidOperationException("A parede selecionada nao possui uma curva de localizacao valida.");

            Curve curve = locationCurve.Curve;
            if (!(curve is Line))
                throw new InvalidOperationException(
                    "Esta versao do algoritmo so suporta paredes retas. Paredes curvas serao tratadas em uma fase futura.");

            double espacamentoInterno = RevitVersionAdapter.CentimetrosParaInterno(espacamentoCm);
            double comprimentoTotal = curve.Length;

            if (comprimentoTotal <= 0.0)
                throw new InvalidOperationException("A parede selecionada tem comprimento invalido (zero ou negativo).");

            var pontos = new List<(XYZ, bool)>();

            int numeroDeSegmentos = (int)Math.Floor(comprimentoTotal / espacamentoInterno);

            pontos.Add((curve.Evaluate(0.0, true), true));

            for (int i = 1; i <= numeroDeSegmentos; i++)
            {
                double distanciaAcumulada = i * espacamentoInterno;
                double t = distanciaAcumulada / comprimentoTotal;
                pontos.Add((curve.Evaluate(t, true), false));
            }

            pontos.Add((curve.Evaluate(1.0, true), true));

            return pontos;
        }

        /// <summary>
        /// Igual a CalcularPontosMontantes, mas so dentro de um trecho do eixo da parede -- usado
        /// para um painel. O primeiro e o ultimo ponto sao os montantes de extremidade do trecho;
        /// entre eles vem a grade normal, que COMECA na ponta inicial do trecho e anda de
        /// espacamento em espacamento. As distancias sao medidas a partir do inicio da parede, em
        /// unidades internas do Revit.
        /// </summary>
        /// <param name="wall">Parede reta.</param>
        /// <param name="espacamentoCm">Espacamento nominal entre montantes, em centimetros.</param>
        /// <param name="inicioDistInterno">Distancia do inicio da parede ao comeco do trecho.</param>
        /// <param name="fimDistInterno">Distancia do inicio da parede ao fim do trecho.</param>
        public List<(XYZ Ponto, bool EhExtremidade)> CalcularPontosMontantesNoIntervalo(
            Wall wall, double espacamentoCm, double inicioDistInterno, double fimDistInterno)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall), "Parede nao pode ser nula.");

            LocationCurve locationCurve = wall.Location as LocationCurve;
            if (locationCurve == null)
                throw new InvalidOperationException("A parede selecionada nao possui uma curva de localizacao valida.");

            Curve curve = locationCurve.Curve;
            if (!(curve is Line))
                throw new InvalidOperationException(
                    "Esta versao do algoritmo so suporta paredes retas. Paredes curvas serao tratadas em uma fase futura.");

            double comprimentoTotal = curve.Length;
            if (comprimentoTotal <= 0.0)
                throw new InvalidOperationException("A parede selecionada tem comprimento invalido (zero ou negativo).");

            double inicio = Math.Max(0.0, inicioDistInterno);
            double fim = Math.Min(comprimentoTotal, fimDistInterno);
            if (fim <= inicio)
                throw new InvalidOperationException("Trecho invalido: o fim precisa ficar depois do inicio.");

            double espacamentoInterno = RevitVersionAdapter.CentimetrosParaInterno(espacamentoCm);
            double larguraTrecho = fim - inicio;
            int numeroDeSegmentos = (int)Math.Floor(larguraTrecho / espacamentoInterno);

            var pontos = new List<(XYZ, bool)>();

            pontos.Add((curve.Evaluate(inicio / comprimentoTotal, true), true));

            for (int i = 1; i <= numeroDeSegmentos; i++)
            {
                double distancia = inicio + (i * espacamentoInterno);
                pontos.Add((curve.Evaluate(distancia / comprimentoTotal, true), false));
            }

            pontos.Add((curve.Evaluate(fim / comprimentoTotal, true), true));

            return pontos;
        }
    }
}