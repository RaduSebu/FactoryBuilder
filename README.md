# Sprite-uri pentru clădiri plasate

Jocul animează sprite-urile schimbând cadre PNG la fiecare 180 ms. Înlocuiește imaginile păstrând structura și numele `frame_00.png`, `frame_01.png` etc.; toate cadrele trebuie să aibă aceeași dimensiune și fundal transparent. Dimensiunea recomandată este 64×64 px. Un singur cadru PNG produce un sprite static.

- `sprites/miner/`
- `sprites/belt_north/`, `belt_east/`, `belt_south/`, `belt_west/`
- `sprites/iron_smelter/`, `sprites/copper_smelter/`, `sprites/alloy_smelter/`
- `sprites/rotor_press/`, `sprites/pump_assembler/`, `sprites/mainframe_fabricator/`, `sprites/thruster_fabricator/`
- `sprites/wiremill/`, `sprites/gear_press/`, `sprites/circuit_assembler/`
- `sprites/extruder/`, `sprites/motor_assembler/`, `sprites/pcb_fabricator/`
- `sprites/processor_assembler/`, `sprites/rocket_fabricator/`
- `sprites/oil_deposit/`, `sprites/oil_pump/`
- `sprites/rubber_tree/`, `sprites/latex_collector/`, `sprites/glue_mixer/`
- `sprites/tin_deposit/`, `sprites/tin_smelter/`, `sprites/tree/`
- `sprites/pipe_north/`, `pipe_east/`, `pipe_south/`, `pipe_west/`
- `sprites/distillery/`, `sprites/chemical_reactor/`, `sprites/polymerizer/`
- `sprites/cargo_ship/`
- `sprites/rock/`
- `sprites/iron_deposit/`
- `sprites/copper_deposit/`

Benzile au câte un folder pentru fiecare direcție. Bolovanii, copacii și zăcămintele de fier, cupru și staniu au sprite-uri distincte. Fișierele sunt copiate automat lângă executabil la compilare.

Fiecare rețetă de utilaj are acum un nume și o animație separate. Folderele utilajelor folosesc aceeași structură: cadre PNG `frame_00.png`, `frame_01.png` etc. Înlocuiește cadrele din folderul utilajului dorit; păstrează aceeași dimensiune și fundalul transparent pentru animații fără întreruperi. Sunt incluse patru cadre editabile ca sprite-uri de pornire.

## Muzică de fundal

Folderul `music/` conține cinci piese WAV (`track_01.wav`–`track_05.wav`). Jocul alege una aleatoriu la pornire, apoi alege aleatoriu o altă piesă de fiecare dată când se termină cea curentă. Înlocuiește fișierele păstrând numele și calea. Butonul „Muzică pornită/oprită” este disponibil atât în meniul principal, cât și în joc. Fișierele audio sunt copiate automat lângă executabil la compilare.

## Texturi pentru iteme

Fiecare item transportat pe bandă are un PNG separat în `items/`. Înlocuiește imaginea dorită păstrând numele fișierului. Sunt recomandate imagini PNG transparente, pătrate, de 64×64 px.

- `iron_ore.png`, `copper_ore.png`, `tin_ore.png`
- `iron_plate.png`, `copper_plate.png`, `copper_wire.png`
- `gear.png`, `circuit.png`, `frame.png`, `motor.png`, `bronze.png`, `rotor.png`, `pump.png`
- `mainframe.png`, `thruster.png`
- `advanced_circuit.png`, `processor.png`, `rocket_part.png`

Textura apare ca o pictogramă peste banda transportoare și se actualizează odată cu itemul. Fișierele sunt copiate automat lângă executabil la compilare.

## Fluide

Țevile folosesc un singur fluid per celulă și îl mută în direcția săgeții la fiecare tură. Construiește o pompă lângă un zăcământ de petrol și orienteaz-o spre o țeavă. La utilaje, țeava de intrare trebuie să fie orientată spre utilaj, iar cea de ieșire în direcția fluxului. Folosește rețetele din panoul „Producție chimică”: petrol → etanol → etilenă → polietilenă. Construiește colectorul lângă un arbore de cauciuc pentru latex, apoi folosește malaxorul de lipici: latex → lipici. Lipiciul intră în rețeta cadrului ranforsat, iar polietilena este folosită la motoare. Bronzul se topește din minereu de staniu și cupru în Alloy Smelter; Rotor Press transformă plăcile de fier în rotoare, iar bronzul și rotorul formează un pump. Mainframe-ul și thruster-ul sunt produse finale. Minereul de staniu este extras cu minerul și topit în aliaj de lipit lichid; aliajul curge prin țevi și este folosit la circuite și procesoare. Fluidele pot fi livrate la o navă printr-o țeavă pentru venit și pentru obiectivele de nivel.

Sprite-urile fluide unice sunt în `fluids/` (`petroleum.png`, `ethanol.png`, `ethylene.png`, `polyethylene.png`, `latex.png`, `glue.png`, `soldering_alloy.png`). Sprite-urile animabile pentru utilaje și țevile direcționale sunt în directoarele corespunzătoare din `sprites/`.

