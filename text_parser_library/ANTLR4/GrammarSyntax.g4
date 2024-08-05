grammar GrammarSyntax;

program: line* EOF;

line: statement | ifBlock | whileBlock | forBlock | functionDefinition | readFile ;

statement: (assignment| functionCall | returnExpression ) ';';

returnExpression: 'return' expression ;

ifBlock: 'if' '(' expression ')' block ('else' elseIfBlock)?;

elseIfBlock: block | ifBlock;

whileBlock: 'while' '(' expression ')' block;

forBlock: 'for' '(' assignment ';' expression ';' assignment ')' block;

functionDefinition: IDENTIFIER '('  paramList ')' block ;

readFile: 'Read' '(' expression ')' ';';

paramList: (IDENTIFIER (',' IDENTIFIER)*)?;

assignment
    : IDENTIFIER '=' expression                     #equalsAssignment
    | IDENTIFIER '.' IDENTIFIER '=' expression      #functionPropertyAssignment
    | IDENTIFIER decrementOp                        #decrementAssignment
    | IDENTIFIER updateOp expression                #updateAssignment
    ;

decrementOp: '++' | '--';
updateOp: '+=' | '-=' | '*=' | '/=';

functionCall: IDENTIFIER '(' (expression (',' expression)*)? ')';

expression
    : constant                          #constantExpression
    | IDENTIFIER                        #identifierExpression
    | functionCall                      #functionCallExpression
    | trigFunction '(' expression ')'   #trigFunctionExpression
    | mathOperator '(' expression ')'   #mathOperatorExpression
    | '(' expression ')'                #parenthesizedExpression
    | '!' expression                    #notExpression
    | expression powOp expression       #powOpExpression
    | expression multOp expression      #multOpExpression
    | expression addOp expression       #addOpExpression
    | expression compareOp expression   #compareOpExpression
    | expression boolOp expression      #boolOpExpression
    ;


trigFunction: 'sin' | 'cos' | 'tan' | 'acos' | 'asin' | 'atan';
mathOperator: 'round' | 'abs' | 'floor' | 'ceil';

powOp: '^';
multOp: '*' | '/' | '%';
addOp: '+' | '-';
compareOp: '==' | '!=' | '>' | '<' | '>=' | '<=';
boolOp: BOOL_OPERATOR;

BOOL_OPERATOR: '&&' | '||' | '^^';

constant: INTEGER | FLOAT | STRING | BOOL | NULL;

INTEGER: '-'?[0-9]+;
FLOAT: '-'?[0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
NULL: 'null';

block: '{' line* '}';

// Single-line comment
COMMENT_SINGLE: '//' ~[\r\n]* -> skip;

// Multi-line comment
COMMENT_MULTI: '/*' .*? '*/' -> skip;


WS: [ \t\r\n]+ -> skip;
IDENTIFIER: [a-zA-Z_][a-zA-Z0-9_]*;