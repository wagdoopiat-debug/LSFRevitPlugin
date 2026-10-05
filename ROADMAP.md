# Roadmap - Plugin LSF para Revit

## Fase 1 - Infraestrutura [OK]
- [x] Projeto C# (.NET Framework 4.8) criado, referenciando RevitAPI/RevitAPIUI do Revit 2021
- [x] Core/App.cs - aba "LSF Modeler" + botao "Ativar LSF" na faixa de opcoes
- [x] Core/Command.cs - ponto de entrada, selecao de parede
- [x] Core/RevitVersionAdapter.cs - conversao de unidades centralizada

## Fase 2 - Distribuicao de montantes [OK]
- [x] Geometry/WallFraming.cs - distribuicao linear ao longo do eixo, espacamento configuravel
- [x] Geometry/StudSolidBuilder.cs + StudDirectShapeFactory.cs - geometria 3D (caixa simplificada)
- [x] Alinhamento correto nas extremidades da parede

## Fase 3 - Calculo estrutural [OK]
- [x] Domain/Structural/Nbr14762Calculator.cs - flambagem local, distorcional, global + flecha L/300
- [x] 8 testes xUnit passando (Tests/LSFRevitPlugin.Domain.Tests)
- [ ] Coeficientes ainda marcados [VALIDAR] contra o texto oficial da NBR 14762:2010

## Fase 4 - Encontros entre paredes [OK]
- [x] Geometry/WallJunctions.cs - deteccao L (ponta-ponta), T (ponta-meio), + (meio-meio)
- [x] Reforco Duplo (L), Triplo (T), Quadruplo (+) - formulas calibradas e validadas visualmente
- [x] Deduplicacao entre paredes (nao duplica ao processar as duas pontas de um encontro)
- [x] Supressao de montantes normais colidindo com reforcos

## Fase 5 - Vaos (portas/janelas) [EM ANDAMENTO]
- [x] Geometry/OpeningReinforcement.cs - deteccao de portas/janelas ja existentes na parede
- [x] King Studs + Jack Studs + verga (soleira) + contra-verga (topo)
- [x] Supressao de montantes normais dentro do vao
- [ ] Montantes curtos (cripple studs) acima da contra-verga e abaixo da verga/soleira

## Fase 6 - Interface [OK PARCIAL]
- [x] UI/OpcoesGeracaoWindow - escolha de espacamento 40/60cm antes de gerar
- [ ] Mais opcoes na UI (bitola do perfil, tipo de conexao, etc.) - nao iniciado

## Fase 7 - Modulos futuros (nao iniciados)
- [ ] Perfil real "C" com furacao (hoje e caixa simplificada)
- [ ] Telhado (RoofFraming)
- [ ] Laje (FloorFraming, In-Line Framing)
- [ ] Radier (FoundationRadier)
- [ ] Paginacao de placas (PanelSheathing)
- [ ] Exportacao DXF/CAM
- [ ] Lista de materiais (BillOfMaterials)
- [ ] Instalador/assinatura digital
