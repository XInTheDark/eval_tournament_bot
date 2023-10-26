#! /bin/bash
# This is the command to run a SPRT. You use it to test a change to your evaluation function against the old version.
# You need to change a few things:
# - This is a bash script, so on linux (and probably mac), you can simply execute this file.
#   On windows, you should execute the sprt.bat file instead.
# - Install cutechess if you haven't installed it already.
# - You should add cutechess-cli to your PATH or change the command below to the location of cutechess-cli.
# - Change the engine names to something that tells you what change you're testing.
# - If you want, you can change the time control.
#   The default here is 5+0.05 so that the SPRT finishes faster, but the tournament will use a longer TC.
# - Change the concurrency to the number of cores on your computer (you can also set it to one less if you want to continue using your computer).
# - The default opening book is UHO.pgn, also known as the Pohl book. If you want, you can change that, for example to Seb's fen list.
# - The elo1 parameter should be set to something close to the Elo gain you expect:
#   Tests tend to take longer for smaller values, but if you set the value too high, the SPRT could fail even though your change gained Elo.
#   10 should be a good default value when you're getting started, but eventually you may have to change that to something like 5.
# - For a non-regression test (ie testing that you didn't lose Elo), set elo0 to a negative value and elo1 to 0.
# Before executing this command, make sure to recompile your project in *Release* mode (recompiling it in Debug mode won't change anything!)
/usr/games/cutechess-cli -engine name="MyBot" cmd="./Chess-Challenge/bin/Release/net6.0/Chess-Challenge" arg="cutechess uci MyBot" stderr="error.txt" -engine name="EvilBot" cmd="./Chess-Challenge/bin/Release/net6.0/Chess-Challenge" arg="cutechess uci EvilBot" -each proto=uci tc=5+0.05 -concurrency 1 -maxmoves 500 -games 2 -repeat -rounds 5000 -ratinginterval 20 -pgnout games.pgn -openings file="UHO.pgn" format=pgn order=random -sprt elo0=0 elo1=10 alpha=0.05 beta=0.1

