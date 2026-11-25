# Sprite-uri pentru clădiri

Jocul animează sprite-urile schimbând cadre PNG la fiecare 180 ms. Înlocuiește imaginile păstrând structura și numele `frame_00.png`, `frame_01.png` etc.; toate cadrele trebuie să aibă aceeași dimensiune și fundal transparent. Dimensiunea recomandată este 64×64 px. Un singur cadru PNG produce un sprite static.

- `sprites/miner/`
- `sprites/belt_north/`, `belt_east/`, `belt_south/`, `belt_west/`
- `sprites/iron_smelter/`, `sprites/copper_smelter/`
- `sprites/wiremill/`, `sprites/gear_press/`, `sprites/circuit_assembler/`
- `sprites/extruder/`, `sprites/motor_assembler/`, `sprites/pcb_fabricator/`
- `sprites/processor_assembler/`, `sprites/rocket_fabricator/`
- `sprites/cargo_ship/`
- `sprites/rock/`
- `sprites/iron_deposit/`
- `sprites/copper_deposit/`

Benzile au câte un folder pentru fiecare direcție. Bolovanii, zăcămintele de fier și cele de cupru au sprite-uri distincte. Fișierele sunt copiate automat lângă executabil la compilare.

Fiecare rețetă de utilaj are acum un nume și o animație separate. Folderele utilajelor folosesc aceeași structură: cadre PNG `frame_00.png`, `frame_01.png` etc. Înlocuiește cadrele din folderul utilajului dorit; păstrează aceeași dimensiune și fundalul transparent pentru animații fără întreruperi. Sunt incluse patru cadre editabile ca sprite-uri de pornire.

## Muzică de fundal

Folderul `music/` conține cinci piese WAV (`track_01.wav`–`track_05.wav`). Jocul alege una aleatoriu la pornire, apoi alege aleatoriu o altă piesă de fiecare dată când se termină cea curentă. Înlocuiește fișierele păstrând numele și calea. Butonul „Muzică pornită/oprită” este disponibil atât în meniul principal, cât și în joc. Fișierele audio sunt copiate automat lângă executabil la compilare.

## Texturi pentru iteme

Fiecare item transportat pe bandă are un PNG separat în `items/`. Înlocuiește imaginea dorită păstrând numele fișierului. Sunt recomandate imagini PNG transparente, pătrate, de 64×64 px.

- `iron_ore.png`, `copper_ore.png`
- `iron_plate.png`, `copper_plate.png`, `copper_wire.png`
- `gear.png`, `circuit.png`, `frame.png`, `motor.png`
- `advanced_circuit.png`, `processor.png`, `rocket_part.png`

Textura apare ca o pictogramă peste banda transportoare și se actualizează odată cu itemul. Fișierele sunt copiate automat lângă executabil la compilare.
