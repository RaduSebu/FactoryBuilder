# Sprite-uri pentru clădiri

Jocul animează sprite-urile schimbând cadre PNG la fiecare 180 ms. Înlocuiește imaginile păstrând structura și numele `frame_00.png`, `frame_01.png` etc.; toate cadrele trebuie să aibă aceeași dimensiune și fundal transparent. Dimensiunea recomandată este 64×64 px. Un singur cadru PNG produce un sprite static.

- `sprites/miner/`
- `sprites/belt_north/`, `belt_east/`, `belt_south/`, `belt_west/`
- `sprites/smelter/`
- `sprites/assembler/`
- `sprites/cargo_ship/`
- `sprites/rock/`
- `sprites/iron_deposit/`
- `sprites/copper_deposit/`

Benzile au câte un folder pentru fiecare direcție. Bolovanii, zăcămintele de fier și cele de cupru au sprite-uri distincte. Fișierele sunt copiate automat lângă executabil la compilare.

## Muzică de fundal

Folderul `music/` conține cinci piese WAV (`track_01.wav`–`track_05.wav`). Jocul alege una aleatoriu la pornire, apoi alege aleatoriu o altă piesă de fiecare dată când se termină cea curentă. Înlocuiește fișierele păstrând numele și calea. Butonul „Muzică pornită/oprită” este disponibil atât în meniul principal, cât și în joc. Fișierele audio sunt copiate automat lângă executabil la compilare.
