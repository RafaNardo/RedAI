# M1 — Backend gaps

- O backend gera e persiste a estratégia, mas não expõe uma leitura da estratégia por campanha. Portanto, o frontend não pode substituir os valores atualmente exibidos na etapa Strategy pela resposta persistida.
- As versões criativas podem ser consultadas apenas por item de conteúdo. Não existe uma consulta agregada de criativos por campanha com os dados necessários para a grade de revisão. O frontend não deve inventar esse resultado; a etapa mantém o estado transitório já usado pelo fluxo atual.
- O endpoint de revisão criativa inicia um job, mas não materializa uma nova versão no handler atual. O frontend aguarda o job e não tenta fabricar uma versão criativa da API.
