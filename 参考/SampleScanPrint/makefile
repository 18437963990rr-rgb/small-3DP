INCLUDE=-I. -I../../Shared

SOURCES= main.cpp

ODIR=gccobj
OBJS = $(patsubst %,$(ODIR)/%,$(SOURCES:%.cpp=%.o))

samplescanprint : $(OBJS)
	gcc $(OBJS) -lm -lstdc++ -lmeteor -lpthread -std=c++11 -o samplescanprint 

$(ODIR)/%.o : %.cpp
	@mkdir -p $(@D)
	gcc -c -Wall -std=c++11 $(INCLUDE) $< -o $@
