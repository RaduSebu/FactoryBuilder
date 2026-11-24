# Factory Frontier

Joc desktop nativ scris în C#, cu fereastră GTK 4 pe Linux. Nu folosește browser sau server web.

## Pornire

Ai nevoie de .NET 10 SDK și GTK 4 instalate. Pe distribuțiile Debian/Ubuntu, biblioteca GTK se instalează cu `sudo apt install libgtk-4-1`.

```sh
dotnet run --project FactoryBuilder
```

## Cum se joacă

Selectează minerul sau banda în panoul din dreapta și plasează-l pe hartă. Alege rețeta în laborator, apoi plasează utilajul indicat. Conectează utilajele cu benzi orientate în direcția săgeții. Timpul avansează automat o dată pe secundă; folosește Pauză pentru a opri simularea sau `+1 tură` / Space pentru un pas manual.

Unealta „Demolează” șterge mineri, benzi și utilaje, inclusiv obiectele aflate în ele. Zăcămintele, bolovanii și nava rămân pe hartă.

Pentru fiecare nivel, livrează de trei ori produsul cerut afișat în bara de progres. Asta deblochează rețeta următoare; livrările altor produse aduc bani, dar nu sar peste progresia rețetelor. Sunt zece niveluri și zece rețete:

1. Nivelul 1: placă de fier
2. Nivelul 2: placă de cupru
3. Nivelul 3: fir de cupru
4. Nivelul 4: roată dințată
5. Nivelul 5: circuit
6. Nivelul 6: cadru ranforsat
7. Nivelul 7: motor
8. Nivelul 8: circuit avansat
9. Nivelul 9: procesor
10. Nivelul 10: piesă de rachetă

Costuri: miner $25, bandă $2, topitor $30, asamblor $45. Orice produs finit livrat se vinde indiferent de nivel; prețul lui este afișat lângă rețetă. Nivelul stabilește progresul rețetelor, nu eligibilitatea pentru plată.

Lumea se extinde la nesfârșit în toate direcțiile: derulează până la marginea unui sector și se generează următorul. Fiecare sector are zăcăminte Fe și Cu și bolovani generați după seed-ul afișat deasupra terenului. Poți construi nave cargo noi cu $120 pe orice celulă liberă; fiecare navă preia produsele de pe benzile care indică spre ea.
