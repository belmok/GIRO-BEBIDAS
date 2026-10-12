# Giro Bebidas — testes antes de liberar mudanças

Preferência explícita do usuário: sempre que implementar uma mudança, executar testes de bugs antes de entregar ou publicar a versão.

- Compile o projeto e execute verificações funcionais relacionadas à mudança. Corrija os erros encontrados e confira novamente os cenários afetados.
- Verifique regressões nos fluxos que a mudança possa afetar, incluindo valores, salvamento, permissões, estados vazios, cancelamento e limites quando aplicável. Use testes que conferem o comportamento do usuário; não apenas a estrutura da implementação.
- Para interface WPF, execute a conferência nativa e inspecione as capturas. Confira a largura real da janela, telas menores, textos e valores sem cortes, seleção, botões e menus. Para rolagem, confira pequenos deltas, gestos consecutivos e os limites da área.
- Use dados e pastas de teste isolados. Não altere os dados reais do depósito nem as configurações pessoais do Windows para testar.
- Antes de publicar uma versão, execute a suíte de verificações de regras de negócio e confira o aplicativo empacotado, instalador e desinstalador. Confira versão, origem e hashes dos arquivos; confirme que o fluxo do GitHub terminou com sucesso e que o atualizador reconhece a publicação.
- Uma falha impede a publicação até ser investigada e corrigida. Adapte os testes às características reais do ambiente sem remover a validação do comportamento necessário. Preserve tags já enviadas no histórico de versões.
- Informe quais verificações passaram e as limitações materiais. Não afirme que impressoras ou balanças físicas foram testadas quando só houve conferência em software.
- Sempre que publicar uma atualização, informe as novidades, as correções e as verificações específicas daquela versão na descrição da Release do GitHub. Registre o conteúdo em `docs/notas-versoes/<versão>.md`, com o título `# Giro Bebidas <versão>` e as seções `## Novidades`, `## Correções` e `## Verificações`. As instruções de instalação ficam depois dessas informações. Não publique somente o texto genérico de instalação nem reutilize notas de outra versão.
